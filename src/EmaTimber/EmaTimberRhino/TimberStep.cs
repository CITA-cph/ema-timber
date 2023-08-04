using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;


using Rhino;
using Rhino.Geometry;

namespace EmaTimber
{
    [Serializable]
    public class TaggedBrep
    {
        public string Name;
        public Brep Geometry = null;
        public Dictionary<string, List<int>> FaceTags = new Dictionary<string, List<int>>();
        public Vector3d FibreDirection;
        public Plane BasePlane;
        public string StpPath = "";

        public TaggedBrep()
        {
            FibreDirection = Vector3d.XAxis;
        }
    }

    public class TaggedCompound
    {
        public string Name;
        public List<string> SolidNames = new List<string>();
        public Dictionary<string, List<int>> FaceTags = new Dictionary<string, List<int>>();
        public string StpPath = "";

    }

    public static class TimberStep
    {
        public static List<TaggedBrep> ImportTaggedBrep(string path)
        {
            var json = System.IO.File.ReadAllText(path);
            return JsonConvert.DeserializeObject<List<TaggedBrep>>(json);
        }

        public static TaggedCompound ImportTaggedCompound(string path)
        {
            var json = System.IO.File.ReadAllText(path);
            return JsonConvert.DeserializeObject<TaggedCompound>(json);
        }

        public static string ExportTaggedBrep(string directory, RhinoDoc doc, IEnumerable<Guid> obj_ids)
        {
            List<TaggedBrep> taggedBreps = new List<TaggedBrep>();

            foreach (var id in obj_ids)
            {
                var brep_object = doc.Objects.Find(id);
                if (brep_object == null) continue;

                Brep brep = null;

                if (brep_object is Rhino.DocObjects.BrepObject)
                    brep = brep_object.Geometry as Brep;
                else if (brep_object is Rhino.DocObjects.ExtrusionObject)
                    brep = (brep_object.Geometry as Extrusion).ToBrep();

                if (brep == null) throw new Exception("Couldn't wrangle Brep.");

                var name = brep_object.Attributes.Name;
                if (string.IsNullOrEmpty(name))
                    name = string.Format("Brep-{0}", id);

                var tbrep = new TaggedBrep();
                tbrep.Name = name;
                tbrep.Geometry = brep;

                var ud = brep_object.Attributes.UserDictionary;

                if (ud.ContainsKey("face_tags"))
                {
                    var ad = ud.GetDictionary("face_tags");

                    foreach (var key in ad.Keys)
                    {
                        tbrep.FaceTags.Add(key, new List<int>());

                        object obj;
                        ad.TryGetValue(key, out obj);

                        var face_indices = obj as IEnumerable<int>;
                        if (face_indices == null) continue;

                        foreach (var face_index in face_indices)
                        {
                            tbrep.FaceTags[key].Add(face_index);
                        }
                    }
                }

                if (ud.ContainsKey("fibre_direction"))
                {
                    var vec = ud.GetVector3d("fibre_direction");
                    var up = Utility.GetMostPerpendicularCrossVector(brep, vec);

                    var temp = new Plane(Point3d.Origin, vec, Vector3d.CrossProduct(up, vec));

                    Box worldBox;
                    BoundingBox bb = brep.GetBoundingBox(temp, out worldBox);

                    Plane bplane;
                    if (worldBox.Y.Length > worldBox.Z.Length)
                        bplane = new Plane(worldBox.GetCorners()[0], vec, temp.YAxis);
                    else
                        bplane = new Plane(worldBox.GetCorners()[0], vec, temp.ZAxis);

                    tbrep.FibreDirection = vec;
                    tbrep.BasePlane = bplane;
                }
                else
                {
                    tbrep.BasePlane = Plane.WorldXY;

                }

                var stp_options = new Rhino.FileIO.FileStpWriteOptions();
                stp_options.SplitClosedSurfaces = false;

                var stp_path = System.IO.Path.Combine(directory, string.Format("{0}.stp", name));
                var stp_doc = Rhino.RhinoDoc.CreateHeadless("");

                var attr = new Rhino.DocObjects.ObjectAttributes();
                attr.Name = name;
                attr.WireDensity = -1;

                stp_doc.Objects.AddBrep(brep, attr);
                Rhino.FileIO.FileStp.Write(stp_path, stp_doc, stp_options);

                stp_doc.Dispose();

                tbrep.StpPath = stp_path;

                taggedBreps.Add(tbrep);

            }

            var json_path = System.IO.Path.Combine(directory, "fragment_input.json");

            string json = JsonConvert.SerializeObject(taggedBreps, Formatting.Indented);
            System.IO.File.WriteAllText(json_path, json);

            return json_path;
        }
    }
}
