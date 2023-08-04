using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace EmaTimber.Commands
{
    [System.Runtime.InteropServices.Guid("f2f8fa9d-7218-475b-8dcc-edc919e025be")]
    public class AssignFaceStringCommand : Command
    {
        public AssignFaceStringCommand()
        {
            Instance = this;
        }

        public static AssignFaceStringCommand Instance
        {
            get; private set;
        }

        public override string EnglishName
        {
            get { return "EmaAssignFaceString"; }
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var refs = Utility.GetBrepFaces();

            var getname = new Rhino.Input.Custom.GetString();
            getname.AcceptNothing(false);
            getname.Get();
            var name = getname.StringResult();

            if (string.IsNullOrEmpty(name)) return Result.Cancel;

            var map = new Dictionary<Guid, List<BrepFace>>();
            foreach (var fref in refs)
            {
                if (!map.ContainsKey(fref.ObjectId))
                    map[fref.ObjectId] = new List<BrepFace>();

                map[fref.ObjectId].Add(fref.Face());
            }

            foreach (var kv in map)
            {
                var obj = doc.Objects.Find(kv.Key);
                var ud = obj.Attributes.UserDictionary;

                if (!ud.ContainsKey("face_tags"))
                    ud.Set("face_tags", new Rhino.Collections.ArchivableDictionary());

                var ad = ud.GetDictionary("face_tags");

                ad.Set(name, kv.Value.Select(x => x.FaceIndex).ToList());
                ud.Set("face_tags", ad);

                doc.Objects.Select(kv.Key);
            }

            return Result.Success;
        }
    }
}
