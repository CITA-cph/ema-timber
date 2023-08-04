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
    [System.Runtime.InteropServices.Guid("bb836204-2523-4c89-b0b3-f212064b9a8f")]
    public class SetMaterialDirectionCommand : Command
    {
        public SetMaterialDirectionCommand()
        {
            Instance = this;
        }

        public static SetMaterialDirectionCommand Instance
        {
            get; private set;
        }

        public override string EnglishName
        {
            get { return "EmaSetMaterialDirection"; }
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var breps = Utility.GetBreps();
            var vec = Utility.GetVector();

            bool clear = false;

            foreach (Rhino.DocObjects.ObjRef objref in breps)
            {
                var obj = objref.Object();
                var geo = objref.Geometry();

                if (geo == null) continue;

                if (clear)
                {
                    geo.UserDictionary.Clear();
                }

                //geo.UserDictionary.Set("fibre_direction", vec);
                //obj.UserDictionary.Set("fibre_direction", vec);
                //obj.Attributes.UserDictionary.Set("fibre_direction", vec);
                obj.Attributes.SetUserString("material_direction_l", vec.ToString());

                //geo.SetUserString("fibre_direction", string.Format("{0} {1} {2}", vec.X, vec.Y, vec.Z));

                
            }

                
            return Result.Success;
        }
    }
}
