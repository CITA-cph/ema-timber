using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Grasshopper.Kernel.Types.Transforms;
using Rhino;
using Rhino.Geometry;

//using GluLamb;

namespace RawLamb
{
    public abstract class KnotFibreOrientationModel
    {
        public abstract Vector3d CalculateFibreOrientation(Point3d point, Knot knot, Vector3d stem_axis);
    }

    public class ArrayComparer : IComparer<double[]>
    {

        public int Compare(double[] x, double[] y)
        {
            return x[0].CompareTo(y[0]);
        }

    }

    public class FlowlineKnotFibreOrientationModel : KnotFibreOrientationModel
    {
        /// <summary>
        /// Velocity of the rectilinear flow.
        /// </summary>
        public double U;
        /// <summary>
        /// Flow rate from the source and sink
        /// </summary>
        public double q;
        /// <summary>
        /// Ratio of U/q
        /// </summary>
        public double G;
        /// <summary>
        /// Short length of the Rankine oval.
        /// </summary>
        public double Bk;
        /// <summary>
        /// Long length of the Rankin oval.
        /// </summary>
        public double Lk;
        /// <summary>
        /// Distance of the source and the sink to the origin of the coordinate system.
        /// </summary>
        public double a;

        public double aa;

        /// <summary>
        /// Look-up table to speed up getting G for every sample.
        /// </summary>
        private List<double[]> GLUT;

        public FlowlineKnotFibreOrientationModel()
        {

        }

        public static Vector3d Project(Plane plane, Vector3d v) => new Vector3d(v - (plane.ZAxis * Vector3d.Multiply(plane.ZAxis, v)));

        public static double Lerp(double min, double max, double mu) => (max - min) * mu + min;

        public void Initialize(double radius_min, double radius_max, double step)
        {
            int N = (int)Math.Ceiling((radius_max - radius_min) / step);
            GLUT = new List<double[]>();

            for (int i = 0; i < N; ++i)
            {
                GLUT.Add(new double[5]);
                GLUT[i][0] = radius_min + i * step;
                GLUT[i][1] = radius_min + i * step;

                ApproximateG(ref GLUT[i][0], ref GLUT[i][1], out GLUT[i][2], out GLUT[i][3], out GLUT[i][4]);
            }

            GLUT.Sort(new ArrayComparer());
        }

        /// <summary>
        /// Approximate G for a given Bk and Lk.
        /// </summary>
        /// <returns></returns>
        public void ApproximateG(ref double Bk, ref double Lk, out double G, out double a, out double aa)
        {
            // Numerical solver parameters - tweak if you need higher precision
            //const double U = 1.0;                       // freestream speed (used as reference)
            const double tolA = 1e-6;                   // tolerance on 'a' search
            const double tolY = 1e-8;                   // tolerance solving psi(0,y)=0
            const int maxIterA = 60;
            const int maxIterY = 80;

            // safety bounds for 'a' search (must be > 0)
            double aMin = 1e-4;
            double aMax = Math.Max(1.0, Lk) * 10.0; // heuristic upper bound

            // simple helper: compute Q from a and desired Lk using algebraic rearrangement
            Func<double, double, double> Q_from_a_Lk = (ax, Lk) =>
            {
                double halfL = Lk * 0.5;
                // must have ax > 0, and ax^2 - halfL^2 >= 0 for real stagnation points
                double num = ax * ax - halfL * halfL;
                if (num <= 0.0) return double.NaN;
                return Math.PI * U * num / ax;
            };

            // stream function psi(0,y) with source at -a and sink at +a, strength Q
            Func<double, double, double, double> psi0 = (ax, Q, y) =>
            {
                // psi(0,y) = U*y + Q/(2*pi) * ( atan2(y, 0+ a) - atan2(y, 0 - a) )
                // careful with atan2 order: here we used source at -a, sink at +a
                double term = (Q / (2.0 * Math.PI)) * (Math.Atan2(y, ax) - Math.Atan2(y, -ax));
                return U * y + term;
            };

            // Given 'a', compute predicted Bk (2*yRoot) by solving psi(0,y)=0 for positive y.
            Func<double, double, double> PredictedBkForA = (ax, Lk) =>
            {
                double Q = Q_from_a_Lk(ax, Lk);
                if (double.IsNaN(Q) || !double.IsFinite(Q))
                    return double.PositiveInfinity; // mark invalid a

                // Check that stagnation x exists:
                double xs2 = ax * ax - Q * ax / (Math.PI * U);
                if (xs2 <= 0.0)
                    return double.PositiveInfinity; // invalid, no closed oval

                // Solve psi(0,y) = 0 for y>0; use bisection between yLo=0 and yHi large.
                double yLo = 0.0;
                double yHi = Math.Max(1.0, ax * 5.0);

                double fLo = psi0(ax, Q, yLo);
                double fHi = psi0(ax, Q, yHi);

                // If signs don't bracket a root, try increasing yHi a few times
                int expandAttempts = 0;
                while (fLo * fHi > 0.0 && expandAttempts < 10)
                {
                    yHi *= 2.0;
                    fHi = psi0(ax, Q, yHi);
                    expandAttempts++;
                }
                if (fLo * fHi > 0.0)
                    return double.PositiveInfinity; // can't find root => invalid

                double yMid = 0.0;
                for (int iter = 0; iter < maxIterY; ++iter)
                {
                    yMid = 0.5 * (yLo + yHi);
                    double fMid = psi0(ax, Q, yMid);

                    if (Math.Abs(fMid) < tolY)
                        break;

                    if (fLo * fMid <= 0.0)
                    {
                        yHi = yMid;
                        fHi = fMid;
                    }
                    else
                    {
                        yLo = yMid;
                        fLo = fMid;
                    }
                }

                return 2.0 * yMid; // full short axis (Bk)
            };

            // preliminary plausibility checks
            if (Bk <= 0.0 || Lk <= 0.0)
            {
                G = double.NaN; a = double.NaN; aa = double.NaN;
                return;
            }

            // Use bisection on 'a' to match predicted Bk to requested Bk.
            // We assume PredictedBkForA(a) is (roughly) continuous and that we can find a bracket [aMin,aMax].
            double left = aMin, right = aMax;
            double fLeft = PredictedBkForA(left, Lk) - Bk;
            double fRight = PredictedBkForA(right, Lk) - Bk;

            // If both sides are Infinity/invalid, expand right
            int expand = 0;
            while ((double.IsInfinity(fLeft) || double.IsInfinity(fRight) || Math.Sign(fLeft) == Math.Sign(fRight)) && expand < 40)
            {
                // try expand search region
                right *= 1.5;
                left = Math.Max(aMin, left * 0.9);
                fLeft = PredictedBkForA(left, Lk) - Bk;
                fRight = PredictedBkForA(right, Lk) - Bk;
                expand++;
            }

            if (double.IsInfinity(fLeft) || double.IsInfinity(fRight) || Math.Sign(fLeft) == Math.Sign(fRight))
            {
                // failsafe: couldn't bracket a root — fall back to best-effort grid search
                double bestA = double.NaN;
                double bestErr = double.PositiveInfinity;
                for (double ax = aMin; ax <= aMax; ax += (aMax - aMin) / 200.0)
                {
                    double pred = PredictedBkForA(ax, Lk);
                    if (double.IsInfinity(pred)) continue;
                    double err = Math.Abs(pred - Bk);
                    if (err < bestErr)
                    {
                        bestErr = err;
                        bestA = ax;
                    }
                }
                if (double.IsNaN(bestA))
                {
                    // cannot find anything reasonable
                    G = double.NaN; a = double.NaN; aa = double.NaN;
                    return;
                }
                // accept the bestA
                a = bestA;
                double Qbest = Q_from_a_Lk(a, Lk);
                G = Qbest / U;
                aa = a;
                return;
            }

            // standard bisection to refine 'a'
            double aMid = 0.0;
            for (int iter = 0; iter < maxIterA; ++iter)
            {
                aMid = 0.5 * (left + right);
                double fMid = PredictedBkForA(aMid, Lk) - Bk;

                if (Math.Abs(fMid) < tolA)
                    break;

                if (Math.Sign(fMid) == Math.Sign(fLeft))
                {
                    left = aMid;
                    fLeft = fMid;
                }
                else
                {
                    right = aMid;
                    fRight = fMid;
                }
            }

            a = aMid;
            double Qfinal = Q_from_a_Lk(a, Lk);
            G = Qfinal / U;
            aa = a;

            // Final plausibility clamp
            if (!double.IsFinite(G) || !double.IsFinite(a))
            {
                G = double.NaN; a = double.NaN; aa = double.NaN;
            }
        }


        public void GetVelocity(double strength, double xs, double ys, double x, double y, out double u, out double v)
        {
            var a = Math.Pow(x - xs, 2) + Math.Pow(y - ys, 2);
            u = strength / (2 * Math.PI) * (x - xs) / a;
            v = strength / (2 * Math.PI) * (y - ys) / a;
        }

        public double GetStreamFunction(double strength, double xs, double ys, double x, double y)
        {
            return strength / (2 * Math.PI) * Math.Atan2(y - ys, x - xs);
        }

        public override Vector3d CalculateFibreOrientation(Point3d point, Knot knot, Vector3d stem_axis)
        {
            // Find knot radius at closest point
            Geometry.ClosestParametersOfCone(knot.Axis.From, knot.Axis.Direction, knot.Radius, point, out Point3d cp, out Bk);

            // Transform point and stem_axis to local CS
            var knot_plane = new Plane(cp, knot.Axis.Direction);
            var projected = stem_axis - knot_plane.ZAxis * (knot_plane.ZAxis * stem_axis);

            projected = Project(knot_plane, stem_axis);
            projected.Unitize();

            var yaxis = Vector3d.CrossProduct(knot_plane.ZAxis, projected);
            knot_plane = new Plane(cp, projected, yaxis);

            var xform = Transform.PlaneToPlane(knot_plane, Plane.WorldXY);
            point.Transform(xform);
            stem_axis.Transform(xform);


            // Freestream speed
            stem_axis.Unitize();
            double ux = stem_axis.X * U;
            double uy = stem_axis.Y * U;

            // beta pattern for late wood
            double aboveA = 1, aboveB = 0.0;
            double belowA = 1, belowB = 0.0;

            /* MAIN */
            double Lr;

            Lk = Bk * 2;

            /* *************************** */
            // Look up G
            /* *************************** */

            var parameters = new double[] { Bk, 0, 0, 0, 0 };
            int index = GLUT.BinarySearch(parameters, new ArrayComparer());
            if (index >= 0)
            {
                parameters = GLUT[index];
            }
            else
            {
                index = ~index;

                if (index >= GLUT.Count) // bigger neighbour is larger than everything...
                {
                    parameters = GLUT[GLUT.Count - 1];
                }
                else if (index == 0) // bigger neighbour is smaller than everything...
                {
                    parameters = GLUT[0];
                }
                else
                {
                    var before = GLUT[index - 1];
                    var after = GLUT[index];

                    var mu = (Bk - before[0]) / (after[0] - before[0]);
                    for (int i = 1; i < parameters.Length; i++)
                    {
                        parameters[i] = Lerp(before[i], after[i], mu);
                    }
                }
            }

            Lk = parameters[1];
            G = parameters[2];
            a = parameters[3];
            aa = parameters[4];

            /* *************************** */

            double Yn = Bk + G / (2 * Math.PI) * (Math.Atan(Lk / (point.X + aa)) - Math.Atan(Lk / (point.X - aa)));

            if (point.X > 0) // assuming above
                Lr = Lk * aboveA * (1 + aboveB * Yn);
            else // assuming below
                Lr = Lk * belowA * (1 + belowB * Yn);

            double Gnew = G * Lr / Lk;
            double anew = aa * Lr / Lk;

            double strength_source = Gnew;
            double strength_sink = -Gnew;

            double x_source = -anew;
            double y_source = 0.0;    // location of the source

            double x_sink = anew;
            double y_sink = 0.0;    // location of the sink

            // compute the velocity field
            GetVelocity(strength_source, x_source, y_source, point.X, point.Y, out double u_source, out double v_source);

            // compute the stream-function
            double psi_source = GetStreamFunction(strength_source, x_source, y_source, point.X, point.Y);

            // compute the velocity field on the mesh grid
            GetVelocity(strength_sink, x_sink, y_sink, point.X, point.Y, out double u_sink, out double v_sink);

            // compute the stream-function on the grid mesh
            double psi_sink = GetStreamFunction(strength_sink, x_sink, y_sink, point.X, point.Y);

            // superposition of a source and a sink on the freestream
            double u = ux + u_source + u_sink;
            double v = uy + v_source + v_sink;

            var dir = new Vector3d(u, v, stem_axis.Z);
            //var dir = new Vector3d(u, v, 0);

            var xform2 = Transform.PlaneToPlane(Plane.WorldXY, knot_plane);

            //xform.TryGetInverse(out Transform xform2);
            dir.Transform(xform2);
            if (dir.IsZero || !dir.IsValid)
                throw new Exception("Fibre direction failed!");
            return dir;
        }
    }
}
