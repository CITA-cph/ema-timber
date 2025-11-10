/*
 * RawLamb
 * Copyright 2022 Tom Svilans
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

using System;
using System.Linq;
using System.Collections.Generic;

using Rhino.Geometry;
using Grasshopper.Kernel;
using DeepSightCommon;

namespace EmaTimber.GH.Components
{
    public class Cmpt_ExportSTP : GH_Component
    {
        public Cmpt_ExportSTP()
          : base("ExportSTP", "STP",
              "Export Breps as in STP format.",
              EmaTimber.GH.Api.ComponentCategory, "IO")
        {
        }



        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Filepath", "FP", "File path of the MetaLog.", GH_ParamAccess.item);
            pManager.AddBrepParameter("Breps", "B", "Breps to export.", GH_ParamAccess.list);
            pManager.AddTextParameter("Names", "N", "Names of Breps.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Run", "R", "Export.", GH_ParamAccess.item, false);

            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("debug", "d", "Debugging output.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var debug = new List<string>();
            var run = false;

            string m_path = String.Empty;
            var breps = new List<Brep>();
            var names = new List<string>();
            if (!DA.GetData("Run", ref run) || !run) return;

            DA.GetData("Filepath", ref m_path);
            DA.GetDataList("Breps", breps);
            DA.GetDataList("Names", names);

            if (m_path == String.Empty || breps.Count < 1) 
            {
                debug.Add("Failed to get Breps or path is not set.");
                DA.SetDataList("debug", debug);
                return; 
            }

            var doc = Rhino.RhinoDoc.CreateHeadless("");

            var options = new Rhino.FileIO.FileStpWriteOptions();
            options.SplitClosedSurfaces = true;
            options.ExportBlack = true;
            options.Export2dCurves = false;

            for (int i = 0; i < breps.Count; ++i)
            {
                var attr = new Rhino.DocObjects.ObjectAttributes();
                attr.Name = i < names.Count ? names[i] : "RhinoBrep";
                doc.Objects.Add(breps[i], attr);

                debug.Add(string.Format("Added brep with name {0} to doc.", attr.Name));
            }

            debug.Add(string.Format("Added {0} breps.", breps.Count));

            bool res = Rhino.FileIO.FileStp.Write(m_path, doc, options);

            doc.Dispose();

            debug.Add(string.Format("Exported STP file to '{0}'. Success: {1}", m_path, res));

            DA.SetDataList("debug", debug);

        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //return Properties.Resources.GridLoad_01;
                return null;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("39592e80-db21-4b58-80cf-4c16eff79e7c"); }
        }
    }
}