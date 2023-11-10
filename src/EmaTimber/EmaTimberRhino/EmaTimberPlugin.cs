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
using EmaTimberRhino;
using EmaTimberRhino.Properties;

namespace EmaTimber
{
    public partial class EmaTimberPlugin: Rhino.PlugIns.PlugIn

    {
        public EmaTimberPlugin()
        {
            if (Instance == null) Instance = this;


            Rhino.Display.DisplayPipeline.PostDrawObjects += ETContext.DisplayPipeline_PostDrawObjects;
            Rhino.Display.DisplayPipeline.CalculateBoundingBox += ETContext.DisplayPipeline_CalculateBoundingBox;
            //Rhino.Display.DisplayPipeline.DrawForeground += ETContext.DisplayPipeline_PostDrawObjects;
        }

        ~EmaTimberPlugin()
        {
            Rhino.Display.DisplayPipeline.PostDrawObjects -= ETContext.DisplayPipeline_PostDrawObjects;
            Rhino.Display.DisplayPipeline.CalculateBoundingBox -= ETContext.DisplayPipeline_CalculateBoundingBox;
            //Rhino.Display.DisplayPipeline.DrawForeground -= ETContext.DisplayPipeline_PostDrawObjects;
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
            Rhino.RhinoApp.WriteLine("EmaTimber");
            Panels.RegisterPanel(this, typeof(EmaTimberPanel), "EmaTimber", Resources.EmaTimber_01);
            Panels.RegisterPanel(this, typeof(SimulationPanel), "FEA", Resources.EmaTimber_01);
            Rhino.RhinoApp.WriteLine(this.Name, this.Version);

            ETContext.MeshMaterial = new Rhino.Display.DisplayMaterial();
            ETContext.MeshMaterial.Specular = System.Drawing.Color.Black;
            ETContext.MeshMaterial.Diffuse = System.Drawing.Color.LightGray;

            return base.OnLoad(ref errorMessage);
        }
    }
}
