using CaeGlobals;
using CaeMesh;
using CaeModel;
using Rhino.NodeInCode;
using Rhino;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GmshCommon;

namespace RawLamAllocator
{

    internal partial class Allocator
    {

        public FeModel ConstructFeModel(RhinoDoc rhinoDoc)
        {
            Dictionary<int, double[]> nodes;
            Dictionary<int, int[]> elements;
            Dictionary<int, int> elementTypes;

            Dictionary<string, List<int>> nodeGroups;
            Dictionary<string, List<Tuple<int, int>>> elementGroups;

            double size_min = Settings.FeMeshSizeMin;
            double size_max = Settings.FeMeshSizeMax;
            var model = new FeModel("EmaObservatory");
            model.Properties.ModelSpace = ModelSpaceEnum.ThreeD;
            model.Properties.ModelType = ModelType.GeneralModel;
            model.UnitSystem = new CaeGlobals.UnitSystem(UnitSystemType.M_KG_S_C);

            var mesher = new Meshing(this);
            try
            {
                mesher.Run(rhinoDoc, Components.Select(x => x.Geometry).ToList(), Components.Select(x => x.Name).ToList(),
                    out nodes, out elements, out nodeGroups, out elementGroups, out elementTypes, size_min, size_max);
            }
            catch (Exception ex)
            {
                Logger.Error(Gmsh.Logger.GetLastError());
                Logger.Error("");
                Logger.Error("");
                Logger.Error("Meshing failed. Continuing to allocation, but don't expect any results.");
                Logger.Error("");
                Logger.Error("");

                //throw (ex);
                return model;
            }

            foreach (var kvp in nodes)
            {
                model.Mesh.Nodes.Add(kvp.Key, new FeNode(kvp.Key, kvp.Value));
            }

            foreach (var kvp in elements)
            {
                if (Array.IndexOf(kvp.Value, 0) > -1)
                    Logger.Error("Zero index found: element {0} ({1})", kvp.Key, string.Join(", ", kvp.Value));

                switch (elementTypes[kvp.Key])
                {
                    case (4):
                        model.Mesh.Elements.Add(kvp.Key, new LinearTetraElement(kvp.Key, 1, kvp.Value));
                        break;
                    case (11):
                        // Gmsh has the 9th and 10th node Ids the wrong way around for CalculiX... so swap them.
                        var temp = kvp.Value[9];
                        kvp.Value[9] = kvp.Value[8];
                        kvp.Value[8] = temp;
                        model.Mesh.Elements.Add(kvp.Key, new ParabolicTetraElement(kvp.Key, 1, kvp.Value));
                        break;
                    default:
                        break;
                }
            }

            Logger.Info("FeModel nNodes: {0}", model.Mesh.Nodes.Count);
            Logger.Info("FeModel nElements: {0}", model.Mesh.Elements.Count);


            model.Mesh.Parts["MainPart"] =
                new CaeMesh.MeshPart("MainPart", 1, nodes.Keys.ToArray(), elements.Keys.ToArray(), model.Mesh.Elements.Values.Select(x => x.GetType()).ToArray());

            foreach (var egroup in elementGroups)
            {

                var eset = new FeElementSet(egroup.Key, egroup.Value.Select(x => x.Item2).Where(x => model.Mesh.Elements.ContainsKey(x)).ToArray());
                model.Mesh.AddElementSet(eset);
                Logger.Info("ElementSet: {0}", eset.Name);
            }

            Logger.Info("Creating material definition...");

            var material = new CaeModel.Material("Spruce");
            material.AddProperty(new EngineeringConstants(
                new double[] { 9700e6, 400e6, 220e6 }, new double[] { 0.35, 0.6, 0.55 }, new double[] { 400e6, 250e6, 25e6 }));
            material.AddProperty(new Density(new double[][] { new double[] { 450.0 } }));
            model.Materials.Add(material.Name, material);

            Logger.Info("Added material '{0}'", material);
            Logger.Info("Adding material orientations...");
            model.Mesh.ElementOrientations.Add(1, new FeMaterialOrientation(1, new double[] { 0, 0, 1 }, new double[] { 0, 1, 0 }));
            model.Mesh.ElementOrientations.Add(2, new FeMaterialOrientation(3, new double[] { 0, 0, 1 }, new double[] { 0, 1, 0 }));
            model.Mesh.ElementOrientations.Add(3, new FeMaterialOrientation(2, new double[] { 0, 0, 1 }, new double[] { 0, 1, 0 }));

            // Make distribution
            var distribution = new FeDistribution("dist", new FeMaterialOrientation(-1, 1, 0, 0, 0, 1, 0), model.Mesh.ElementOrientations.Select(x => x.Key).ToArray());
            model.Mesh.Distributions.Add("dist", distribution);
            var orientation = new FeOrientation("orientation", distribution);
            model.Mesh.Orientations.Add(orientation.Name, orientation);

            var section = new SolidSection("Section", material.Name, "MainPart", RegionTypeEnum.PartName, 10.0, false);
            section.Orientation = orientation;
            model.Sections.Add(section.Name, section);
            Logger.Info("Added section '{0}'", section);

            var gravityLoad = new GravityLoad("Gravity", "MainPart", RegionTypeEnum.PartName, 0, 0, -9.8, false, false, 0);
            var step = new StaticStep("Step1", true);
            step.AddLoad(gravityLoad);

            var supportNodes = Allocator.GetNodesOnMesh(rhinoDoc, model, Supports, step, 0.001);
            //foreach (var entry in model.Mesh.Nodes)
            //{
            //if (entry.Value.Z < 0.001)
            //    supportNodes.Add(entry.Value.Id);
            //}

            var supportNodeSet = new FeNodeSet("Supports", supportNodes.ToArray());
            model.Mesh.NodeSets.Add(supportNodeSet.Name, supportNodeSet);
            var supportBC = new FixedBC("Supports", supportNodeSet.Name, RegionTypeEnum.NodeSetName, false);

            step.AddBoundaryCondition(supportBC);

            model.StepCollection.AddStep(step, true);

            ValidMesh = true;
            return model;
        }

    }
}
