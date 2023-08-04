using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharpPcap.LibPcap;

using Rhino.PlugIns;
using Rhino.Geometry;
using Rhino.UI;
using SharpPcap;

namespace EmaTimber
{
    public partial class EmaTimberPlugin: Rhino.PlugIns.PlugIn

    {
        public EmaTimberPlugin()
        {
            if (Instance == null) Instance = this;

            Rhino.Display.DisplayPipeline.PostDrawObjects += ETContext.DisplayPipeline_PostDrawObjects;
            Rhino.Display.DisplayPipeline.CalculateBoundingBox += ETContext.DisplayPipeline_CalculateBoundingBox;
        }

        ~EmaTimberPlugin()
        {
            Rhino.Display.DisplayPipeline.PostDrawObjects -= ETContext.DisplayPipeline_PostDrawObjects;
            Rhino.Display.DisplayPipeline.CalculateBoundingBox -= ETContext.DisplayPipeline_CalculateBoundingBox;

        }

        public override object GetPlugInObject()
        {
            return Instance;
        }

        ///<summary>Gets the only instance of the RNNPlugin plug-in.</summary>
        public static EmaTimberPlugin Instance
        {
            get; private set;
        }

        // You can override methods here to change the plug-in behavior on
        // loading and shut down, add options pages to the Rhino _Option command
        // and mantain plug-in wide options in a document.

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {

            Rhino.RhinoApp.WriteLine(this.Name, this.Version);

            return base.OnLoad(ref errorMessage);
        }
    }
}
