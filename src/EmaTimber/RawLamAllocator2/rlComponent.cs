extern alias destination;

using Rhino.Geometry;
using destination::StudioAvw.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClipperLib;

namespace RawLamAllocator
{
    internal class rlComponent
    {
        public Plane Plane;
        public Brep Geometry;
        public string Name;
        public Polyline[] Outline;

        public rlComponent()
        {
            Plane = Plane.Unset;
            Geometry = null;
            Name = "null";
            Outline = null;
        }

        public rlComponent(Plane plane, Brep geometry, string name)
        {
            Plane = plane;
            Geometry = geometry;
            Name = name;
            Outline = null;
        }

        public static Polyline[] MeshOutline(Mesh mesh)
        {
            double tolerance = 0.001;
            var up = Vector3d.ZAxis;
            var faces = new List<Polyline>();
            mesh.FaceNormals.ComputeFaceNormals();

            Point3f a, b, c, d;
            for (int i = 0; i < mesh.Faces.Count; ++i)
            {
                var face = mesh.Faces[i];
                if (mesh.FaceNormals[i] * up == 0) continue;
                mesh.Faces.GetFaceVertices(i, out a, out b, out c, out d);

                if (face.IsTriangle)
                    faces.Add(new Polyline() { a, b, c, a });
                else
                    faces.Add(new Polyline() { a, b, c, d, a });
            }

            var faceArray = faces.ToArray();

            if (faceArray.Length < 1)
                return new Polyline[0];

            var facesA = new Polyline[] { faceArray[0] };

            var res = facesA;
            for (int i = 1; i < faces.Count; ++i)
            {
                res = Polyline3D.Boolean(destination::ClipperLib.ClipType.ctUnion, res, new Polyline[] { faces[i] }, Plane.WorldXY, tolerance, false).ToArray();
            }

            return res;
        }

        public static Polyline UnionOffset(IList<Polyline> polys, double offset=0.0)
        {
            //var polys = Polyline3D.ConvertCurvesToPolyline(curves).ToList();

            if (polys.Count < 2)
            {
                return polys[0];
            }

            var res = new Polyline[] { polys[0] };

            for (int i = 1; i < polys.Count; ++i)
            {
                res = Polyline3D.Boolean(destination::ClipperLib.ClipType.ctUnion, res, new Polyline[] { polys[i] }, Plane.WorldXY, 0.001, false).ToArray();
            }

            List<Polyline> outContour, outHole;
            Polyline3D.Offset(res, Polyline3D.OpenFilletType.Butt, Polyline3D.ClosedFilletType.Miter, offset, Plane.WorldXY, 0.001, out outContour, out outHole);

            if (outContour != null && outContour.Count > 0)
                res = outContour.ToArray();

            var biggestIndex = 0;
            var biggestArea = 0.0;

            for (int i = 0; i < res.Length; ++i)
            {
                var amp = AreaMassProperties.Compute(res[i].ToNurbsCurve());
                if (amp == null) continue;

                if (amp.Area > biggestArea)
                {
                    biggestIndex = i;
                    biggestArea = amp.Area;
                }
            }

            var output = res[biggestIndex];
            for (int i = 0; i < output.Count; ++i)
            {
                output[i] = new Point3d(output[i].X, output[i].Y, 0);
            }

            return output;
        }
    }
}
