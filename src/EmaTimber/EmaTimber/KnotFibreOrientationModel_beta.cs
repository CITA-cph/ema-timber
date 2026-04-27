using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;

namespace RawLamb
{
    /// <summary>
    /// Orthotropic material frame: L = fibre (grain) direction,
    /// R = radial, T = tangential.
    /// </summary>
    public struct OrthoFrame
    {
        public Vector3d L; // Longitudinal  (fibre / grain)
        public Vector3d R; // Radial
        public Vector3d T; // Tangential
    }

    /// <summary>
    /// Pre-computed Rankine-oval parameters for one knot radius sample.
    /// </summary>
    public struct RankineParams
    {
        public double Bk;  // Short (radial) half-axis of the oval
        public double Lk;  // Long  (axial)  half-axis of the oval
        public double G;   // Source/sink strength  q = G * U
        public double a;   // Source/sink offset from origin
    }

    public static class RankineOval
    {
        // ------------------------------------------------------------------ //
        //  Rankine-oval geometry                                              //
        //                                                                     //
        //  Given the oval half-width Bk (= knot radius), solve for (a, G):  //
        //                                                                     //
        //    Bk satisfies:  Bk² + a² = G·a / π          … (1)              //
        //    Lk satisfies:  Lk  = a · tan(π·Lk / G)     … (2)              //
        //                                                                     //
        //  Strategy:                                                          //
        //    • Sweep a ∈ (0, Bk) and derive G from (1): G = π(Bk²+a²)/a    //
        //    • For each (a,G) pair solve (2) for Lk by bisection.            //
        //    • Choose the (a,G,Lk) triple that makes the oval most           //
        //      "natural" — here we target Lk ≈ 2·Bk (aspect ratio ≈ 2).    //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Given the knot's cross-sectional radius <paramref name="Bk"/>,
        /// compute the Rankine-oval parameters (Lk, G, a).
        /// </summary>
        public static RankineParams ComputeParams(double Bk, double targetAspect, int maxIter = 25)
        {
            double targetLk = Bk * targetAspect;
            double aLo = Bk * 1e-3;
            double aHi = Bk * 0.9999;

            for (int i = 0; i < 60; i++)
            {
                double aMid = 0.5 * (aLo + aHi);
                double G = Math.PI * (Bk * Bk + aMid * aMid) / aMid;
                double Lk = SolveLk(aMid, G);

                if (double.IsNaN(Lk) || Lk < targetLk)
                    aHi = aMid;
                else
                    aLo = aMid;

                if (aHi - aLo < 1e-12 * Bk) break;
            }

            double aFinal = 0.5 * (aLo + aHi);
            double GFinal = Math.PI * (Bk * Bk + aFinal * aFinal) / aFinal;
            double LkFinal = SolveLk(aFinal, GFinal, maxIter);

            return new RankineParams { Bk = Bk, Lk = LkFinal, G = GFinal, a = aFinal };
        }

        /// <summary>
        /// Bisection solver for  Lk = a · tan(π·Lk/G)  on (0, G/2 - ε).
        /// </summary>
        private static double SolveLk(double a, double G, int maxIter = 25)
        {
            // f(x) = x - a*tan(π*x/G)
            double lo = 1e-10;
            double hi = G / 2.0 - G * 1e-6;   // stay away from the asymptote

            double fLo = lo - a * Math.Tan(Math.PI * lo / G);
            double fHi = hi - a * Math.Tan(Math.PI * hi / G);

            if (fLo * fHi > 0) return double.NaN; // no sign change → no root

            for (int i = 0; i < maxIter; ++i)
            {
                if (hi - lo < 1e-10) break;
                
                double mid = 0.5 * (lo + hi);
                double fMid = mid - a * Math.Tan(Math.PI * mid / G);

                if (fLo * fMid <= 0) { hi = mid; fHi = fMid; }
                else { lo = mid; fLo = fMid; }

                if (hi - lo < 1e-12 * G) break;
            }

            return 0.5 * (lo + hi);
        }

        // ------------------------------------------------------------------ //
        //  Flow velocity from a point source/sink in 2-D                     //
        // ------------------------------------------------------------------ //

        public static void PointSourceVelocity(
            double strength, double xs, double ys,
            double x, double y,
            out double u, out double v)
        {
            double dx = x - xs;
            double dy = y - ys;
            double r2 = dx * dx + dy * dy;
            double fac = strength / (2.0 * Math.PI * r2);
            u = fac * dx;
            v = fac * dy;
        }

        // ------------------------------------------------------------------ //
        //  Rankine-oval velocity at a point in the knot's local 2-D frame.  //
        //                                                                     //
        //  The freestream (grain direction projected onto the knot plane)    //
        //  travels in the +X direction with unit speed.  The source sits at  //
        //  (-a, 0) and the sink at (+a, 0).                                  //
        // ------------------------------------------------------------------ //

        public static (double u, double v) OvalVelocity(
            RankineParams p,
            double x, double y,
            double ux, double uy)      // freestream direction (unit vector components)
        {
            PointSourceVelocity(p.G, -p.a, 0.0, x, y, out double us, out double vs);
            PointSourceVelocity(-p.G, p.a, 0.0, x, y, out double uk, out double vk);

            return (ux + us + uk, uy + vs + vk);
        }
    }

    // ---------------------------------------------------------------------- //
    //  Look-up table: one RankineParams per sample radius                    //
    // ---------------------------------------------------------------------- //

    public class RankineLUT
    {
        private readonly double[] _radii;
        private readonly RankineParams[] _params;

        public RankineLUT(double rMin, double rMax, int n, double aspectRatio = 2.0, int maxIter = 25)
        {
            _radii = new double[n];
            _params = new RankineParams[n];

            for (int i = 0; i < n; ++i)
            {
                double r = rMin + (rMax - rMin) * i / (n - 1);
                _radii[i] = r;
                _params[i] = RankineOval.ComputeParams(r, aspectRatio, maxIter);
            }
        }

        /// <summary>Linear-interpolated lookup by knot radius.</summary>
        public RankineParams Lookup(double Bk)
        {
            int i = Array.BinarySearch(_radii, Bk);
            if (i >= 0) return _params[i];

            i = ~i;                                   // insertion point

            if (i == 0) return _params[0];
            if (i >= _radii.Length) return _params[_radii.Length - 1];

            double t = (Bk - _radii[i - 1]) / (_radii[i] - _radii[i - 1]); // fixed sign
            var lo = _params[i - 1];
            var hi = _params[i];

            return new RankineParams
            {
                Bk = Bk,
                Lk = lo.Lk + t * (hi.Lk - lo.Lk),
                G = lo.G + t * (hi.G - lo.G),
                a = lo.a + t * (hi.a - lo.a),
            };
        }
    }

    // ---------------------------------------------------------------------- //
    //  Main model                                                             //
    // ---------------------------------------------------------------------- //

    public class FlowlineKnotFibreOrientationModel_beta : KnotFibreOrientationModel
    {
        private readonly RankineLUT _lut;

        /// <param name="rMin">Minimum knot radius in the dataset.</param>
        /// <param name="rMax">Maximum knot radius in the dataset.</param>
        /// <param name="lutSamples">Resolution of the pre-computed table.</param>
        /// <param name="aspectRatio">Target Lk/Bk ratio (default 2).</param>
        public FlowlineKnotFibreOrientationModel_beta(
            double rMin = 0.005,
            double rMax = 0.10,
            int lutSamples = 64,
            double aspectRatio = 2.0,
            int maxIter = 25)
        {
            _lut = new RankineLUT(rMin, rMax, lutSamples, aspectRatio, maxIter);
        }

        /// <summary>
        /// Compute the orthotropic frame at <paramref name="samplePoint"/>
        /// for a list of knots, superposing all their flow contributions.
        /// </summary>
        /// <param name="samplePoint">World-space sample point.</param>
        /// <param name="knots">All knots in the board/log.</param>
        /// <param name="globalGrainAxis">Nominal grain direction (world space).</param>
        public OrthoFrame CalculateFrame(
            Point3d samplePoint,
            IList<Knot> knots,
            Vector3d globalGrainAxis)
        {
            // Accumulate 3-D velocity contributions from every knot.
            // We start from the nominal (undisturbed) grain direction.
            globalGrainAxis.Unitize();
            var totalDir = globalGrainAxis;

            foreach (var knot in knots)
            {
                if (knot.Contains(samplePoint) > 0)
                {
                    var L = knot.Axis.Direction;
                    var cp = knot.Axis.ClosestPoint(samplePoint, false);
                    var R = samplePoint - cp;

                    L.Unitize();
                    R.Unitize();
                    var T = Vector3d.CrossProduct(L, R);
                    return new OrthoFrame { L = L, R = R, T = T };
                }

                totalDir += KnotVelocityContribution(samplePoint, knot, globalGrainAxis);
            }

            // Build the orthotropic frame from the resulting fibre direction.
            return BuildFrame(totalDir, globalGrainAxis);
        }

        // ------------------------------------------------------------------ //

        private Vector3d KnotVelocityContribution(
            Point3d P,
            Knot knot,
            Vector3d grainAxis)
        {
            // 1. Find the closest point on the knot cone and the local radius.
            Geometry.ClosestParametersOfCone(
                knot.Axis.From, knot.Axis.Direction, knot.Radius,
                P, out Point3d cp, out double Bk);

            if (Bk <= 0) return Vector3d.Zero;   // degenerate knot

            // 2. Build a local 2-D coordinate system centred at cp.
            //    X  = projected grain direction (freestream)
            //    Y  = perpendicular in the plane normal to the knot axis
            //    Z  = knot axis direction (out-of-plane, no flow component)
            var knotAxis = knot.Axis.Direction;
            knotAxis.Unitize();

            // Project grain onto plane perpendicular to knot axis.
            var freestreamDir = grainAxis - knotAxis * (knotAxis * grainAxis);
            if (freestreamDir.IsZero) return Vector3d.Zero;  // grain || knot axis
            freestreamDir.Unitize();

            var yAxis = Vector3d.CrossProduct(knotAxis, freestreamDir);
            var knotPlane = new Plane(cp, freestreamDir, yAxis);

            // 3. Express the sample point in the knot's local 2-D frame.
            var xform = Transform.PlaneToPlane(knotPlane, Plane.WorldXY);
            var Pl = P;
            Pl.Transform(xform);

            double px = Pl.X;
            double py = Pl.Y;

            // 4. Look up Rankine parameters for this radius.
            var rp = _lut.Lookup(Bk);

            // 5. Compute 2-D Rankine-oval velocity (freestream = +X, unit speed).
            var (u, v) = RankineOval.OvalVelocity(rp, px, py, 1.0, 0.0);

            // 6. Perturbation relative to undisturbed freestream (1, 0).
            double du = u - 1.0;
            double dv = v;

            // 7. Map the 2-D perturbation back to world space.
            //    The local frame has X = freestreamDir, Y = yAxis.
            var worldPerturbation = freestreamDir * du + yAxis * dv;

            return worldPerturbation;
        }

        // ------------------------------------------------------------------ //

        private static OrthoFrame BuildFrame(Vector3d fibre, Vector3d referenceUp)
        {
            fibre.Unitize();

            // Radial: perpendicular to fibre and as close as possible to referenceUp
            var R = referenceUp - fibre * (fibre * referenceUp);
            if (R.IsZero)
            {
                // referenceUp is parallel to fibre — pick any perpendicular
                R = fibre.X != 0 || fibre.Y != 0
                    ? new Vector3d(-fibre.Y, fibre.X, 0)
                    : new Vector3d(1, 0, 0);
            }
            R.Unitize();

            var T = Vector3d.CrossProduct(fibre, R);
            T.Unitize();

            return new OrthoFrame { L = fibre, R = R, T = T };
        }

        public override Vector3d CalculateFibreOrientation(Point3d point, Knot knot, Vector3d stem_axis)
        {
            throw new NotImplementedException();
        }
    }
}