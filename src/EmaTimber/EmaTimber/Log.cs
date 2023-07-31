using System;
using System.Collections.Generic;

using Rhino.Geometry;
using StudioAvw.Geometry;

using DeepSight;
using DeepSight.Rhino;

using Grid = DeepSight.FloatGrid;
using System.Linq;

namespace RawLamb
{
    public class Log
    {
        public Guid Id;
        public string Name;
        public Rhino.Geometry.Mesh Mesh;
        public Plane Plane;
        public List<Knot> Knots;
        public Polyline Pith;
        public BoundingBox BoundingBox;
        public Dictionary<string, GridApi> Grids;
        public RTree Tree;
        private List<KnotRegion> m_knot_regions;

        public List<Board> Boards;

        public bool IsTreeInitialized
        {
            get
            {
                return Tree != null;
            }
        }

        public Log()
        {
            Id = Guid.NewGuid();
            Boards = new List<Board>();
            Knots = new List<Knot>();
            Plane = Plane.WorldXY;
            Mesh = new Rhino.Geometry.Mesh();
            Pith = new Polyline();
            Grids = new Dictionary<string, GridApi>();
        }

        public Log(string name, Grid ctlog) : base()
        {
            Name = name;
        }

        public void ReadInfoLog(string path)
        {
            InfoLog ilog = new InfoLog();
            ilog.Read(path);

            Pith = new Polyline();

            for (int i = 0; i < ilog.Pith.Length; i += 2)
            {
                Pith.Add(new Point3d(ilog.Pith[i], ilog.Pith[i + 1], i * 5));
            }

            Knots = new List<Knot>();


            for (int i = 0; i < ilog.Knots.Length; i += 11)
            {
                var buf = ilog.Knots;

                var line = new Line(
                    new Point3d(
                        buf[i + 1],
                        buf[i + 2],
                        buf[i + 3]),
                    new Point3d(
                        buf[i + 4],
                        buf[i + 5],
                        buf[i + 6]));

                var knot = new Knot()
                {
                    Index = (int)buf[i],
                    Axis = line,
                    Length = line.Length,
                    DeadKnotRadius = ilog.Knots[i + 7],
                    Radius = ilog.Knots[i + 8],
                    Volume = ilog.Knots[i + 10]
                };

                Knots.Add(knot);
            }
        }

        [Obsolete("Not used any more")]
        public static Log LoadCtLog(string path, Transform xform, bool create_mesh = false, double resample_resolution = 30.0, double mesh_isovalue = 0.7)
        {
            if (!System.IO.File.Exists(path))
                throw new Exception($"File '{path}' does not exist.");

            string name = System.IO.Path.GetFileNameWithoutExtension(path);

            Grid ctlog = null;

            try
            {
                ctlog = GridIO.Read(path)[0] as FloatGrid;
                if (ctlog == null) throw new ArgumentException("Could not get a FloatGrid.");
            }
            catch (Exception e)
            {
                throw new Exception($"Loading CtLog failed: {e.Message}");
            }

            ctlog.Transform(xform);

            var log = new Log(name, ctlog);

            if (create_mesh)
            {
                var rlog = Tools.Resample(ctlog, resample_resolution);
                log.Mesh = rlog.ToRhinoMesh(mesh_isovalue, true);
            }

            return log;
        }

        public void Transform(Transform xform)
        {
            if (Mesh != null)
                Mesh.Transform(xform);

            Plane.Transform(xform);
            for (int i = 0; i < Boards.Count; ++i)
            {
                Boards[i].Transform(xform);
            }
            foreach (var key in Grids.Keys)
            {
                Grids[key].Transform(xform);
            }
            foreach (var knot in Knots)
                knot.Transform(xform);

            Pith.Transform(xform);
        }

        public Log Duplicate()
        {
            var log = new Log() { Name = Name, Plane = Plane, Pith = Pith.Duplicate(), BoundingBox = BoundingBox };

            if (this.Mesh != null)
                log.Mesh = this.Mesh.DuplicateMesh();

            for (int i = 0; i < Boards.Count; ++i)
            {
                var brd = Boards[i].Duplicate();
                brd.Log = log;
                log.Boards.Add(brd);
            }

            for (int i = 0; i < Knots.Count; ++i)
            {
                log.Knots.Add(Knots[i].Duplicate());
            }
            foreach (var key in Grids.Keys)
            {
                log.Grids[key] = Grids[key].Duplicate();
            }

            return log;
        }

        /// <summary>
        /// IN PROGRESS.
        /// </summary>
        /// <param name="cutting_planes"></param>
        /// <param name="names"></param>
        /// <param name="kerf"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public List<Board> CutBoards(IList<Plane> cutting_planes, IList<string> names, double kerf = 3.0, double offset = 0.0)
        {
            var boards = new List<Board>();

            for (int i = 0; i < cutting_planes.Count - 1; ++i)
            {
                string name = string.Format("Board_{0:00}", i);
                if (names != null && i < names.Count)
                {
                    name = names[i];
                }

                var brd = CutBoard(cutting_planes[i], cutting_planes[i + 1], name, offset);
                boards.Add(brd);
            }

            Boards.AddRange(boards);
            return boards;
        }

        public Board CutBoard(Plane top, Plane bottom, string name = "Board", double offset = 0.0)
        {
            if (Mesh == null)
                throw new Exception("Log has no mesh.");

            Vector3d v = top.Origin - bottom.Origin;

            double thickness = Math.Abs(v * top.ZAxis);
            var brd = new Board() { Name = name, Thickness = thickness, Plane = top, LogId = Id, Log = this };

            if (top.ZAxis * v < 0)
                top = new Plane(top.Origin, -top.XAxis, top.YAxis);
            if (bottom.ZAxis * v > 0)
                bottom = new Plane(bottom.Origin, -bottom.XAxis, bottom.YAxis);

            brd.TopPlane = top;
            brd.BottomPlane = bottom;

            // Intersect planes with log mesh


            int index = 0;
            double max_area = 0;
            List<Polyline> pout1, pout2;

            var res = Rhino.Geometry.Intersect.Intersection.MeshPlane(Mesh, top);
            if (res != null && res.Length > 0)
            {


                if (res.Length > 1)
                {
                    for (int j = 0; j < res.Length; ++j)
                    {
                        var amp = AreaMassProperties.Compute(res[j].ToNurbsCurve());
                        if (amp == null) continue;

                        if (amp.Area > max_area)
                        {
                            index = j;
                            max_area = amp.Area;
                        }
                    }
                }

                Polyline3D.Offset(new Polyline[] { res[index] },
                  Polyline3D.OpenFilletType.Butt, Polyline3D.ClosedFilletType.Miter,
                  offset,
                  brd.Plane,
                  0.01,
                  out pout1,
                  out pout2);

                if (pout1.Count > 0)
                    brd.Top = pout1;
                else if (pout2.Count > 0)
                    brd.Top = pout2;
                else
                    brd.Top = new List<Polyline> { res[index] };
            }

            // Cut bottom plane
            res = Rhino.Geometry.Intersect.Intersection.MeshPlane(Mesh, bottom);
            if (res != null && res.Length > 0)
            {
                index = 0;
                max_area = 0;

                if (res.Length > 1)
                {
                    for (int j = 0; j < res.Length; ++j)
                    {
                        var amp = AreaMassProperties.Compute(res[j].ToNurbsCurve());
                        if (amp == null) continue;

                        if (amp.Area > max_area)
                        {
                            index = j;
                            max_area = amp.Area;
                        }
                    }
                }

                Polyline3D.Offset(new Polyline[] { res[index] },
                  Polyline3D.OpenFilletType.Butt, Polyline3D.ClosedFilletType.Miter,
                  offset,
                  brd.Plane,
                  0.01,
                  out pout1,
                  out pout2);

                if (pout1.Count > 0)
                    brd.Bottom = pout1;
                else if (pout2.Count > 0)
                    brd.Bottom = pout2;
                else
                    brd.Bottom = new List<Polyline> { res[index] };
            }

            Boards.Add(brd);

            return brd;
        }

        public Board CutBoard(Plane p, string name = "Board", double thickness = 45.0, double offset = 0.0)
        {
            if (Mesh == null)
                throw new Exception("Log has no mesh.");

            var brd = new Board() { Name = name, Thickness = thickness, Plane = p, LogId = Id, Log = this };

            var res = Rhino.Geometry.Intersect.Intersection.MeshPlane(Mesh, p);
            if (res == null || res.Length < 1) return null;

            int index = 0;
            double max_area = 0;

            if (res.Length > 1)
            {
                for (int j = 0; j < res.Length; ++j)
                {
                    var amp = AreaMassProperties.Compute(res[j].ToNurbsCurve());
                    if (amp == null) continue;

                    if (amp.Area > max_area)
                    {
                        index = j;
                        max_area = amp.Area;
                    }
                }
            }

            List<Polyline> pout1, pout2;
            Polyline3D.Offset(new Polyline[] { res[index] },
              Polyline3D.OpenFilletType.Butt, Polyline3D.ClosedFilletType.Miter,
              offset,
              brd.Plane,
              0.01,
              out pout1,
              out pout2);

            if (pout1.Count > 0)
                brd.Centre = pout1[0];
            else if (pout2.Count > 0)
                brd.Centre = pout2[0];
            else
                brd.Centre = res[index];

            brd.Centre.ReduceSegments(3);
            brd.LogId = Id;

            Boards.Add(brd);

            return brd;
        }

        public T[] Sample<T>(Rhino.Geometry.Mesh mesh, string key, int sample_type = 0)
        {
            if (!Grids.ContainsKey(key))
            {
                return null;
            }

            var grid = Grids[key];

            if (grid.GetType().GetGenericArguments()[0] != typeof(T))
                return null;

            var tgrid = grid as GridBase<T>;

            var coords = mesh.Vertices.ToFloatArray();

            return tgrid.GetValuesWorld(Array.ConvertAll(coords, x => (double)x));
            //return this.Grids[key].GetValuesWorld(Array.ConvertAll(coords, x => (double)x));
        }

        public void ConstructRTree()
        {
            var knot_region_geometry = new List<Brep>();
            m_knot_regions = new List<KnotRegion>();

            foreach (Knot knot in Knots)
            {
                var knot_region = new KnotRegion(knot, 6, 35);
                m_knot_regions.Add(knot_region);
                knot_region_geometry.Add(knot_region.ToBrep());
            }

            Tree = new RTree();

            for (int i = 0; i < Knots.Count; ++i)
            {
                Tree.Insert(knot_region_geometry[i].GetBoundingBox(true), i);
            }
        }

        public void GetMaterialOrientations(LogModel logModel, KnotFibreOrientationModel knotModel, IList<Point3d> samplePoints, out Plane[] orientations)
        {
            orientations = new Plane[samplePoints.Count];
            int[] sampleStatus, knotIndices;
            SampleLog(logModel, samplePoints, out sampleStatus, out knotIndices);

            var radii = Knots.Select(x => x.Radius);
            var min_radius = radii.Min();
            var max_radius = radii.Max();
            
            if (knotModel is FlowlineKnotFibreOrientationModel)
                (knotModel as FlowlineKnotFibreOrientationModel).Initialize(min_radius * 0.8, max_radius * 1.2, (max_radius - min_radius) / 100);

            for (int i = 0; i < samplePoints.Count; ++i)
            {
                switch(sampleStatus[i])
                {
                    case (-1):
                        orientations[i] = logModel.GetMaterialDirection(samplePoints[i]);
                        break;
                    case (0):
                        orientations[i] = logModel.GetMaterialDirection(samplePoints[i]);
                        break;
                    default:
                        var knot = (Knot)Knots.Where(x => x.Index == sampleStatus[i]).First();
                        if (knot.Contains(samplePoints[i]) >= 0)
                        {
                            orientations[i] = new Plane(samplePoints[i], knot.Axis.Direction, knot.Axis.From - samplePoints[i]);
                            break;
                        }
                        var xaxis = knotModel.CalculateFibreOrientation(samplePoints[i], knot, logModel.Plane.ZAxis);
                        var yaxis = logModel.Plane.ZAxis;
                        var oriPlane = new Plane(samplePoints[i], xaxis, yaxis);
                        if (!oriPlane.IsValid) throw new Exception("Orientation plane not valid!");
                        orientations[i] = oriPlane;
                        break;
                }
            }

        }

        public void SampleLog(LogModel logModel, IList<Point3d> samplePoints, out int[]sampleStatus, out int[] knotIndices)
        {
            if (!IsTreeInitialized)
                ConstructRTree();

            sampleStatus = new int[samplePoints.Count];
            knotIndices = new int[samplePoints.Count];

            for (int i = 0; i < samplePoints.Count; ++i)
            {
                var point = samplePoints[i];

                /*
                if (logModel.Contains(point) < 0)
                {
                    status.Add(-1); // Sample is outside of log
                    continue;
                }
                */

                var sphere = new Sphere(point, 10.0);
                var found = new List<int>();

                Tree.Search(sphere, (Object sender, RTreeEventArgs args) =>
                {
                    found.Add(args.Id);
                }
                  );

                if (found.Count < 1)
                {
                    sampleStatus[i] = 0; // Sample is in clearwood
                    continue;
                }

                var inside = new List<int>();

                // Now find if they are actually within the knot regions
                for (int j = 0; j < found.Count; ++j)
                {
                    var krdata = m_knot_regions[found[j]];
                    if (krdata.Contains(point) > 0)
                        inside.Add(found[j]);
                }

                if (inside.Count > 0)
                {
                    sampleStatus[i] = Knots[inside[0]].Index; // Sample is inside of a knot region
                }
                else
                {
                    sampleStatus[i] = 0; // Sample is inside of clearwood
                }
            }
        }

        public override string ToString()
        {
            return $"Log ({Name})";
        }
    }
}
