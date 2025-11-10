/*
 * EmaTimber.GH
 * Copyright 2025 Tom Svilans
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * 
 */

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using RawLamb;
using Rhino;
using Rhino.Geometry;
using System;

namespace EmaTimber.GH.Components
{
    public class Cmpt_LogKnotOrientations : GH_Component
    {
        public Cmpt_LogKnotOrientations()
            : base("Log Orientations", "LOri", "Define element orientations according to a timber log model including knots.", Api.ComponentCategory, "Model")
        {
        }
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        Line[] linesL = null;
        Line[] linesR = null;
        double lineLength = 0.02; // meters
        double defaultKnotRadius = 0.007; // meters

        double TransitionZoneFactor = 1.5;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Log axis", "LA", "Line representing central axis of the log.", GH_ParamAccess.item);
            pManager.AddCurveParameter("Knot axes", "KA", "Lines representing knot axes.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Knot radii", "KR", "Radii of each knot.", GH_ParamAccess.list);

            pManager[1].Optional = true;
            pManager[2].Optional = true;

            pManager.AddPointParameter("Points", "P", "Sample points.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("U", "U", "Flow speed.", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("a", "a", "a dist.", GH_ParamAccess.item, 1.0);

        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPlaneParameter("Orientations", "O", "Orientations as planes.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var lineLengthActual = RhinoMath.UnitScale(UnitSystem.Meters, RhinoDoc.ActiveDoc.ModelUnitSystem) * lineLength;

            Curve logAxis = null;
            List<Curve> knotsAxes = new List<Curve>();
            List<double> knotsRadii = new List<double>();
            GH_Structure<GH_Point> points;
            double U = 1.0;

            DA.GetData("Log axis", ref logAxis);
            DA.GetDataList("Knot axes", knotsAxes);
            DA.GetDataList("Knot radii", knotsRadii);
            DA.GetData("U", ref U);

            int nKnots = knotsAxes.Count;
            if (knotsRadii.Count > 0)
            {
                nKnots = (int)Math.Min(knotsRadii.Count, knotsAxes.Count);
            }
            else if (nKnots > 0)
            {
                knotsRadii = Enumerable.Repeat(RhinoMath.UnitScale(UnitSystem.Meters, RhinoDoc.ActiveDoc.ModelUnitSystem) * defaultKnotRadius, nKnots).ToList();
            }

            for (int i = 0; i < nKnots; ++i)
            {
                knotsAxes[i].Domain = new Interval(0, 1);
            }

            DA.GetDataTree(3, out points);

            if (logAxis == null) return;

            logAxis.TryGetPolyline(out Polyline logPith);

            var log = new Log();
            log.Name = "Log";
            log.Pith = logPith;
            log.Plane = new Plane(logAxis.PointAtStart, logAxis.TangentAtStart);

            for (int i = 0; i < nKnots; ++i)
            {
                log.Knots.Add(new Knot(i, new Line(knotsAxes[i].PointAtStart, knotsAxes[i].PointAtEnd), knotsRadii[i], knotsRadii[i], knotsAxes[i].GetLength(), 0));
            }

            linesL = new Line[points.Paths.Count];
            linesR = new Line[points.Paths.Count];
            int counter = 0;

            var samples = new List<Point3d>();
            var paths = new List<GH_Path>();

            foreach (GH_Path path in points.Paths)
            {
                var point = points[path][0].Value;
                samples.Add(point);
                paths.Add(path);

                counter++;
            }

            log.GetMaterialOrientations(new SimpleLogModel() { Plane = log.Plane }, new FlowlineKnotFibreOrientationModel() { U = U }, samples, out Plane[] orientations); 
            DataTree<GH_Plane> orientationTree = new DataTree<GH_Plane>();

            for (int i = 0; i < orientations.Length; ++i)
            {
                linesL[i] = new Line(orientations[i].Origin, orientations[i].XAxis, lineLengthActual);
                linesR[i] = new Line(orientations[i].Origin, orientations[i].YAxis, lineLengthActual);

                orientationTree.Add(new GH_Plane(orientations[i]), paths[i]);
            }

            DA.SetDataTree(0, orientationTree);
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (linesL != null)
                args.Display.DrawLines(linesL, System.Drawing.Color.Red, 1);
            if (linesR != null)
                args.Display.DrawLines(linesR, System.Drawing.Color.Lime, 1);
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("6F6A9726-5E04-40EC-AD45-2C8F876D4146"); 
    }
}
