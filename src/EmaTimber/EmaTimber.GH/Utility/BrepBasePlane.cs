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
using System.Collections.Generic;

using Rhino.Geometry;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

using Grid = DeepSight.FloatGrid;

namespace EmaTimber.GH.Components
{
    public class Cmpt_BasePlane : GH_Component
    {
        public Cmpt_BasePlane()
          : base("BasePlane", "BPlane",
              "Calculate a good baseplane for a Brep with straight edges.",
              EmaTimber.GH.Api.ComponentCategory, "Utility")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Brep", "B", "Brep to analyse for base plane.", GH_ParamAccess.item);
            pManager.AddVectorParameter("XAxis", "X", "Optional vector to lock the primary direction (X-axis).", GH_ParamAccess.item, Vector3d.Unset);
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Baseplane", "P", "Best baseplane for Brep.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep brep = null;
            if (!DA.GetData("Brep", ref brep)) return;

            Vector3d xaxis = Vector3d.Unset;
            DA.GetData("XAxis", ref xaxis);

            var bplane = RawLamb.Geometry.FindBestBasePlane(brep, xaxis);

            DA.SetData("Baseplane", bplane);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //return Properties.Resources.GridSample_01;
                return null;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("16d4ad09-a5db-44a0-95ab-1db411b479ff"); }
        }
    }
}