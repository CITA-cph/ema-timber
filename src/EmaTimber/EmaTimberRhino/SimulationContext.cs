using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GmshCommon;
using CaeMesh;
using static GmshCommon.Gmsh;
using Rhino;
using CaeModel;
using CaeGlobals;
using System.Security.Policy;

using Pair = System.Tuple<int, int>;
using Rhino.Display;
using System.Xml.Linq;

namespace EmaTimberRhino
{

    public class SimulationPart
    {
        public Rhino.Geometry.Mesh Mesh { get; set; }
        private Vector3d m_materialDirection;
        public Vector3d MaterialDirection 
        { 
            get 
            {
                return m_materialDirection; 
            }
            set
            {
                m_materialDirection = value;

                int r = (int)((m_materialDirection.X + 1) * 0.5 * 255);
                int g = (int)((m_materialDirection.Y + 1) * 0.5 * 255);
                int b = (int)((m_materialDirection.Z + 1) * 0.5 * 255);

                //RhinoApp.WriteLine("r {0} g {1} b {2}", r, g, b);

                Material.Diffuse = System.Drawing.Color.FromArgb(r, g, b);
                Material.Emission = System.Drawing.Color.FromArgb(r, g, b);
            }
        }
        public DisplayMaterial Material { get; set; }
        public int[] NodeIds { get; set; }

        public SimulationPart(Rhino.Geometry.Mesh mesh) 
        {
            Mesh = mesh;
            Material = new DisplayMaterial { Specular = System.Drawing.Color.Black, Emission = System.Drawing.Color.White };
            MaterialDirection = Vector3d.XAxis;
        }
    }

    public class SimulationContext
    {
        public Rhino.Geometry.Mesh SimulationMesh;
        public FeModel Model;
        public Dictionary<Guid, SimulationPart> Parts = new Dictionary<Guid, SimulationPart>();

        // Settings
        public double MaxElementSize = 100;
        public double MinElementSize = 0;

        public SimulationContext() { }

        public Dictionary<Guid, SimulationPart> GetSimulationParts(FeMesh feMesh, double scale = 1.0, string[] parts = null)
        {
            if (parts == null)
                parts = feMesh.Parts.Keys.ToArray();

            Dictionary<Guid, SimulationPart> simParts = new Dictionary<Guid, SimulationPart>();

            int i = 0;

            foreach (string part in parts)
            {
                if (!Guid.TryParse(part, out Guid partId)) continue;
                var simPart = new SimulationPart(new Rhino.Geometry.Mesh());

                Double[][] nodeCoor;
                Int32[] cellIds;
                Int32[][] cells;
                Int32[] cellTypes;
                Int32[] nodeIds;

                feMesh.GetVisualizationNodesAndCells(feMesh.Parts[part], out nodeIds, out nodeCoor, out cellIds, out cells, out cellTypes);
                var faces = feMesh.GetVisualizationFaceIds(nodeIds, cellIds, false, false, true);

                simPart.NodeIds = nodeIds;
                foreach (var node in nodeCoor)
                    simPart.Mesh.Vertices.Add(
                        new Point3d(
                        node[0] * scale,
                        node[1] * scale,
                        node[2] * scale));

                foreach (var cell in cells)
                    simPart.Mesh.Faces.AddFace(cell[0], cell[1], cell[2]);

                simPart.Mesh.SetUserString("part_name", part);

                var rhinoObject = RhinoDoc.ActiveDoc.Objects.FindId(partId);
                if (rhinoObject != null) 
                {
                    var userString = rhinoObject.Attributes.GetUserString("material_direction_l");
                    if (!string.IsNullOrEmpty(userString))
                    {
                        var tok = userString.Split(',');
                        var vector = new Vector3d(double.Parse(tok[0]), double.Parse(tok[1]), double.Parse(tok[2]));
                        vector.Unitize();

                        //RhinoApp.WriteLine("material direction for {0} is {1} (parsed from '{2}')", partId, vector, userString);

                        simPart.MaterialDirection = vector;
                    }
                }



                simParts[partId] = simPart;

                ++i;
            }

            return simParts;
        }

        public void ConstructFeModel()
        {
            Dictionary<int, double[]> nodes;
            Dictionary<int, int[]> elements;
            Dictionary<int, int> elementTypes;

            Dictionary<string, List<int>> nodeGroups;
            Dictionary<string, List<Tuple<int, int>>> elementGroups;

            double size_min = 100; // Settings.FeMeshSizeMin;
            double size_max = 1; // Settings.FeMeshSizeMax;
            Model = new FeModel("FeModel");
            Model.Properties.ModelSpace = ModelSpaceEnum.ThreeD;
            Model.Properties.ModelType = ModelType.GeneralModel;
            Model.UnitSystem = new CaeGlobals.UnitSystem(UnitSystemType.M_KG_S_C);

            ExtractMesh(out nodes, out elements, out elementTypes, out nodeGroups, out elementGroups);

            foreach (var kvp in nodes)
            {
                Model.Mesh.Nodes.Add(kvp.Key, new FeNode(kvp.Key, kvp.Value));
            }

            foreach (var kvp in elements)
            {
                if (Array.IndexOf(kvp.Value, 0) > -1)
                    RhinoApp.WriteLine("Zero index found: element {0} ({1})", kvp.Key, string.Join(", ", kvp.Value));

                switch (elementTypes[kvp.Key])
                {
                    case (4):
                        Model.Mesh.Elements.Add(kvp.Key, new LinearTetraElement(kvp.Key, 1, kvp.Value));
                        break;
                    case (11):
                        // Gmsh has the 9th and 10th node Ids the wrong way around for CalculiX... so swap them.
                        var temp = kvp.Value[9];
                        kvp.Value[9] = kvp.Value[8];
                        kvp.Value[8] = temp;
                        Model.Mesh.Elements.Add(kvp.Key, new ParabolicTetraElement(kvp.Key, 1, kvp.Value));
                        break;
                    default:
                        break;
                }
            }

            var partId = 1;

            foreach (var egroup in elementGroups)
            {
                var eset = new FeElementSet(egroup.Key, egroup.Value.Select(x => x.Item2).Where(x => Model.Mesh.Elements.ContainsKey(x)).ToArray());
                Model.Mesh.AddElementSet(eset);
                RhinoApp.WriteLine("ElementSet: {0}", eset.Name);

                var nodeIds = Model.Mesh.GetNodeIdsFromElementSet(eset);

                var meshPart = new CaeMesh.MeshPart(eset.Name, partId, nodeIds, egroup.Value.Select(x => x.Item1).ToArray(), eset.Labels.Select(x => Model.Mesh.Elements[x].GetType()).ToArray());

                Model.Mesh.Parts[eset.Name] = meshPart;

                meshPart.Visualization.ExtractVisualizationCellsFromElements3D(Model.Mesh.Elements, eset.Labels);

                Model.Mesh.ExtractSolidPartVisualization(meshPart, 0.1);
                partId++;
            }

            //Model.Mesh.Parts["MainPart"] =
            //    new CaeMesh.MeshPart("MainPart", partId, nodes.Keys.ToArray(), elements.Keys.ToArray(), Model.Mesh.Elements.Values.Select(x => x.GetType()).ToArray());

            //BasePart[] modifiedParts, newParts;
            //Model.Mesh.CreatePartsFromElementSets(elementGroups.Select(x => x.Key).ToArray(), out modifiedParts, out newParts);

            //foreach (var part in newParts)
            //{
            //    Model.Mesh.Parts.Add(part.Name, part);
            //}
            //foreach (var part in modifiedParts)
            //{
            //    Model.Mesh.Parts.Add(part.Name, part);
            //}

            Parts = GetSimulationParts(Model.Mesh);

            RhinoApp.WriteLine("Creating material definition...");

            var material = new CaeModel.Material("Spruce");
            material.AddProperty(new EngineeringConstants(
                new double[] { 9700e6, 400e6, 220e6 }, new double[] { 0.35, 0.6, 0.55 }, new double[] { 400e6, 250e6, 25e6 }));
            material.AddProperty(new Density(new double[][] { new double[] { 450.0 } }));
            Model.Materials.Add(material.Name, material);

            RhinoApp.WriteLine("Added material '{0}'", material);
            RhinoApp.WriteLine("Adding material orientations...");

            foreach (var eset in Model.Mesh.ElementSets)
            {
                Guid brepId = Guid.Parse(eset.Key);
                if (brepId == null) { continue; }

                var rhinoObject = RhinoDoc.ActiveDoc.Objects.FindId(brepId);
                if (rhinoObject == null) { continue; }

                var userString = rhinoObject.Attributes.GetUserString("material_direction_l");
                if (string.IsNullOrEmpty(userString)) { continue; }

                var tok = userString.Split(',');
                var vector = new Vector3d(double.Parse(tok[0]), double.Parse(tok[1]), double.Parse(tok[2]));

                var up = Math.Abs(vector * Vector3d.ZAxis) < 1.0 ? Vector3d.ZAxis : Vector3d.YAxis;

                foreach (var ele in eset.Value.Labels)
                {
                    Model.Mesh.ElementOrientations.Add(1, new FeMaterialOrientation(ele, 
                        new double[] { vector.X, vector.Y, vector.Z }, new double[] { up.X, up.Y, up.Z }));
                }
            }

            // Make distribution
            var distribution = new FeDistribution("dist", new FeMaterialOrientation(-1, 1, 0, 0, 0, 1, 0), Model.Mesh.ElementOrientations.Select(x => x.Key).ToArray());
            Model.Mesh.Distributions.Add("dist", distribution);
            var orientation = new FeOrientation("orientation", distribution);
            Model.Mesh.Orientations.Add(orientation.Name, orientation);

            var section = new SolidSection("Section", material.Name, "MainPart", RegionTypeEnum.PartName, 10.0, false);
            section.Orientation = orientation;
            Model.Sections.Add(section.Name, section);
            RhinoApp.WriteLine("Added section '{0}'", section);

            var gravityLoad = new GravityLoad("Gravity", "MainPart", RegionTypeEnum.PartName, 0, 0, -9.8, false, false, 0);
            var step = new StaticStep("Step1", true);
            step.AddLoad(gravityLoad);
        }

        public static void ExtractMesh(
            out Dictionary<int, double[]> nodes, out Dictionary<int, int[]> elements, out Dictionary<int, int> elementTypes,
            out Dictionary<string, List<int>> nodeGroups, out Dictionary<string, List<Pair>> elementGroups)
        {

            nodes = new Dictionary<int, double[]>();
            elements = new Dictionary<int, int[]>();
            elementTypes = new Dictionary<int, int>();

            // Get all nodes
            IntPtr[] nodeTagsIntPtr;
            double[] coords;
            Gmsh.Mesh.GetNodes(out nodeTagsIntPtr, out coords, 3, -1, true, false);

            var nodeDict = new Dictionary<int, Tuple<double, double, double>>();
            for (int i = 0; i < nodeTagsIntPtr.Length; ++i)
            {
                var nodeTag = (int)nodeTagsIntPtr[i];
                var nodeCoord = new double[] { coords[i * 3], coords[i * 3 + 1], coords[i * 3 + 2] };
                nodes[nodeTag] = nodeCoord;
            }

            // Get all elements
            int[] elementTypesTemp;
            IntPtr[][] elementTagsIntPtr;
            IntPtr[][] elementNodeTags;
            Gmsh.Mesh.GetElements(out elementTypesTemp, out elementTagsIntPtr, out elementNodeTags, 3, -1);

            for (int i = 0; i < elementTypesTemp.Length; ++i)
            {
                var elementType = elementTypesTemp[i];
                if (elementType != 4 && elementType != 11) continue;

                int dim, order, numNodes, numPrimaryNodes;
                string elementName;
                double[] localNodeCoords;

                elementName = GmshCommon.Gmsh.Mesh.GetElementProperties(elementType, out dim, out order, out numNodes, out localNodeCoords, out numPrimaryNodes);

                for (int j = 0; j < elementTagsIntPtr[i].Length; ++j)
                {
                    var elementTag = (int)elementTagsIntPtr[i][j];
                    elementTypes[elementTag] = elementType;
                    var elementNodes = new int[numNodes];

                    for (int k = 0; k < numNodes; ++k)
                    {
                        if ((int)elementNodeTags[i][j * numNodes + k] == 0) RhinoApp.WriteLine("Node ID can't be 0");
                        elementNodes[k] = (int)elementNodeTags[i][j * numNodes + k];
                    }
                    elements[elementTag] = elementNodes;
                }
            }

            nodeGroups = new Dictionary<string, List<int>>();
            elementGroups = new Dictionary<string, List<Pair>>();

            // Get all physical groups
            var pgMap = new Dictionary<string, List<Pair>>();
            var physicalGroups = Gmsh.GetPhysicalGroups(3);
            foreach (var pg in physicalGroups)
            {
                var pgName = Gmsh.GetPhysicalName(pg.Item1, pg.Item2);
                if (!elementGroups.ContainsKey(pgName))
                    elementGroups[pgName] = new List<Pair>();

                var entities = Gmsh.GetEntitiesForPhysicalGroup(pg.Item1, pg.Item2);

                foreach (var entity in entities)
                {
                    Gmsh.Mesh.GetElements(out elementTypesTemp, out elementTagsIntPtr, out elementNodeTags, pg.Item1, entity);
                    foreach (var ele in elementTagsIntPtr)
                        elementGroups[pgName].AddRange(ele.Select(x => new Pair(pg.Item1, (int)x)));
                }
            }
        }

    }
}
