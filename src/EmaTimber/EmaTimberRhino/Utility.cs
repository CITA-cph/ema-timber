using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino;
using Rhino.Geometry;

namespace EmaTimber
{
    public static class Utility
    {
        public static Rhino.DocObjects.ObjRef[] GetBreps()
        {
            string msg = "Select a Brep";

            var go = new Rhino.Input.Custom.GetObject();
            go.SetCommandPrompt(msg);
            go.GeometryFilter = Rhino.DocObjects.ObjectType.Brep;
            go.AcceptNothing(false);

            if (go.GetMultiple(1, 100) != Rhino.Input.GetResult.Object) return null;

            return go.Objects();
        }

        public static Rhino.DocObjects.ObjRef[] GetBrepFaces()
        {
            string msg = "Select a surface or Brep face";
            var go = new Rhino.Input.Custom.GetObject();
            go.SetCommandPrompt(msg);
            go.GeometryFilter = Rhino.DocObjects.ObjectType.Surface;
            go.GeometryAttributeFilter = Rhino.Input.Custom.GeometryAttributeFilter.SubSurface;
            go.SubObjectSelect = true;
            go.AcceptNothing(false);

            if (go.GetMultiple(1, 100) != Rhino.Input.GetResult.Object) return null;

            return go.Objects();
        }

        public static Vector3d GetVector()
        {
            Line line;
            Rhino.Input.RhinoGet.GetLine(out line);

            var v = line.Direction;
            v.Unitize();

            return v;
        }

        public static Vector3d GetMostPerpendicularCrossVector(Brep brep, Vector3d fwd)
        {
            var min_dot = 1.0;
            var index = 0;

            var vecs = new Vector3d[brep.Faces.Count];

            for (int i = 0; i < brep.Faces.Count; ++i)
            {
                var face = brep.Faces[i];
                if (face.IsPlanar())
                {
                    Plane plane;
                    face.TryGetPlane(out plane);

                    var dot = Math.Abs(plane.ZAxis * fwd);

                    if (dot < min_dot)
                    {
                        min_dot = dot;
                        index = i;
                        vecs[i] = plane.ZAxis;
                    }
                }
            }

            return vecs[index];
        }


        public static Vector3d GetMostLikelyCrossVector(Brep brep, Vector3d fwd)
        {
            var candidates = new List<Tuple<double, BrepFace, Vector3d>>();

            for (int i = 0; i < brep.Faces.Count; ++i)
            {
                var face = brep.Faces[i];
                if (face.IsPlanar())
                {
                    Plane plane;
                    face.TryGetPlane(out plane);

                    var dot = Math.Abs(plane.ZAxis * fwd);
                    if (dot < RhinoMath.ZeroTolerance)
                    {
                        var amp = AreaMassProperties.Compute(face);
                        candidates.Add(new Tuple<double, BrepFace, Vector3d>(amp.Area, face, plane.ZAxis));
                    }
                }
            }

            if (candidates.Count < 1)
            {
                return Math.Abs(fwd * Vector3d.ZAxis) == 1.0 ? Vector3d.YAxis : Vector3d.ZAxis;
            }

            candidates.Sort((x, y) => x.Item1.CompareTo(y.Item1));
            return candidates[candidates.Count - 1].Item3;
        }
    }
}
