using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using System.Threading.Tasks;

using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

using GmshCommon;
using Pair = System.Tuple<int, int>;

namespace EmaTimber.Commands
{
    [System.Runtime.InteropServices.Guid("b5c14317-4e3b-4e67-8b50-592a1af41e18")]
    public class MeshBrepCommand : Command
    {
        public MeshBrepCommand()
        {
            Instance = this;
        }

        public static MeshBrepCommand Instance
        {
            get; private set;
        }

        public override string EnglishName
        {
            get { return "EmaMeshBrep"; }
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var breps = Utility.GetBreps();

            Gmsh.InitializeGmsh();
            Gmsh.Clear();
            Gmsh.Logger.Start();

            Gmsh.SetNumber("Mesh.ScalingFactor", 1e-5);
            Gmsh.SetNumber("Geometry.OCCParallel", 1);
            Gmsh.SetNumber("Geometry.Tolerance", 1e-3);

            RhinoApp.WriteLine("Starting Gmsh...");


            Pair[] ent3d, ent2d;

            var guidMap = new Dictionary<int, Guid>();

            foreach (Rhino.DocObjects.ObjRef objref in breps)
            {
                var obj = objref.Object();
                var brep = objref.Brep();

                if (brep == null) continue;

                var tag = GmshCommon.GeometryExtensions.AddBrep(brep);
                //RhinoApp.WriteLine("volume tag {0}", tag);
                guidMap[tag] = objref.ObjectId;

                if (brep.IsSolid)
                {
                    Gmsh.OCC.Remove(Gmsh.OCC.GetEntities(2), true);
                    Gmsh.OCC.Synchronize();
                }
                Gmsh.OCC.Remove(Gmsh.OCC.GetEntities(1), true);
                Gmsh.OCC.Synchronize();
                Gmsh.OCC.Remove(Gmsh.OCC.GetEntities(0), true);
                Gmsh.OCC.Synchronize();


            }

            ent3d = Gmsh.OCC.GetEntities(3);
            //RhinoApp.WriteLine("ent3d {0}", ent3d.Length);
            //foreach (var e3d in ent3d)
            //{
            //    RhinoApp.WriteLine("    {0}", e3d);
            //}

            if (ent3d.Length > 1)
            {
                RhinoApp.WriteLine("Got multiple 3d entities: {0}", ent3d.Length);

                var objectTags = new Pair[] { ent3d[0] };
                var toolTags = new Pair[ent3d.Length - 1];
                Array.Copy(ent3d, 1, toolTags, 0, toolTags.Length);

                Pair[] outDimTags, outDimTags2;
                Pair[][] outDimTagsMap;
                Gmsh.OCC.Fragment(objectTags, toolTags, out outDimTags, out outDimTagsMap, -1, true, true);

                //Gmsh.OCC.HealShapes(out outDimTags2, outDimTags, 1e-3, true, true, true, true, true);
                Gmsh.OCC.Synchronize();
                RhinoApp.WriteLine("Fragmenting resulted in {0} volumes:", outDimTags.Length);

                
                for (int i = 0; i < outDimTags.Length; ++i)
                {
                    RhinoApp.WriteLine("    {0}", outDimTags[i]);
                    if (outDimTagsMap[i].Length > 0)
                    {
                        //Gmsh.AddPhysicalGroup(3, outDimTagsMap[0].Select(x => x.Item2).ToArray(), guidMap[objectTags[0].Item2].ToString());
                    }

                }

                if (guidMap.ContainsKey(objectTags[0].Item2) && outDimTagsMap[0].Length > 0)
                {
                    Gmsh.AddPhysicalGroup(3, outDimTagsMap[0].Select(x => x.Item2).ToArray(), guidMap[objectTags[0].Item2].ToString());
                }

                for (int i = 1; i < outDimTagsMap.Length; ++i)
                {
                    if (guidMap.ContainsKey(toolTags[i-1].Item2) && outDimTagsMap[i].Length > 0)
                    {
                        Gmsh.AddPhysicalGroup(3, outDimTagsMap[i].Select(x => x.Item2).ToArray(), guidMap[toolTags[i - 1].Item2].ToString());
                    }
                }
            }
            else
                foreach (var kvp in guidMap)
                {
                    Gmsh.AddPhysicalGroup(3, new int[] { kvp.Key }, kvp.Value.ToString());
                }

            Gmsh.SetNumber("Mesh.ToleranceInitialDelaunay", 1e-8);
            //Gmsh.SetNumber("Mesh.MeshSizeFromCurvature", 6);
            Gmsh.SetNumber("Mesh.MeshSizeMax", ETContext.SimulationContext.MaxElementSize);
            Gmsh.SetNumber("Mesh.MeshSizeMin", ETContext.SimulationContext.MinElementSize);
            Gmsh.Generate(3);


            ent3d = Gmsh.OCC.GetEntities(3);
            ent2d = Gmsh.OCC.GetEntities(2);

            try
            {
                Mesh mesh;
                if (ent3d.Length > 0)
                    mesh = GmshCommon.GeometryExtensions.GetMesh(ent3d);
                else
                    mesh = GmshCommon.GeometryExtensions.GetMesh(ent2d);

                ETContext.SimulationContext.SimulationMesh = mesh;
            }
            catch (Exception e)
            {
                RhinoApp.WriteLine(e.Message);
                RhinoApp.WriteLine(Gmsh.Logger.GetLastError());
            }

            ETContext.SimulationContext.ConstructFeModel();

            FileInOut.Output.CalculixFileWriter.Write("C:/tmp/test_model.inp", ETContext.SimulationContext.Model);

            Gmsh.FinalizeGmsh();

            RhinoApp.WriteLine("Done.");
            return Result.Success;
        }
    }
}
