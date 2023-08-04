using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GmshCommon;
using Pair = System.Tuple<int, int>;
using Grasshopper;

namespace RawLamAllocator
{
    internal partial class Allocator
    {

        public static void DoMeshing(
            RhinoDoc doc, List<Brep> breps, List<string> names, 
            out Dictionary<int, double[]> nodes, out Dictionary<int, int[]> elements,
            out Dictionary<string, List<int>> nodeGroups, out Dictionary<string, List<Pair>> elementGroups,
            out Dictionary<int, int> elementTypes,
            double sizeMin, double sizeMax,
            string export_directory = "C:/tmp")
        {
            var scale = RhinoMath.UnitScale(doc.ModelUnitSystem, UnitSystem.Meters);
            sizeMin = sizeMin * scale;
            sizeMax = sizeMax * scale;

            //try 
            {
                Gmsh.InitializeGmsh();
                Gmsh.Logger.Start();

                Gmsh.Clear();
                Pair[] dimTags;

                var surfaces = new DataTree<int>();
                var volumes = new DataTree<int>();

                var faces = new List<Brep>();
                var faceMap = new Dictionary<string, List<int>>();

                var fragMap = new List<Pair>();

                var volumeMap = new Dictionary<int, string>();

                int[] tags;

                for (int i = 0; i < names.Count; ++i)
                {
                    Logger.Info(names[i]);
                }

                //BrepStepGmshIndividual(temp_dir, allElements, allNames, out tags, scale);
                BrepStepGmsh1(System.IO.Path.Combine(export_directory, "temp.stp"), breps, names, out tags, scale);

                fragMap.AddRange(tags.Select(x => new Pair(3, x)));
                for (int i = 0; i < tags.Length; ++i)
                {
                    volumeMap[tags[i]] = names[i];
                }


                Pair[][] dimTagMap;
                var tools = new Pair[fragMap.Count - 1];
                Array.Copy(fragMap.ToArray(), 1, tools, 0, tools.Length);

                Gmsh.OCC.Fragment(new Pair[] { fragMap[0] }, tools, out dimTags, out dimTagMap, -1, true, true);

                Gmsh.OCC.Synchronize();

                Logger.Info("length dimTagMap {0}", dimTagMap.Length);
                Logger.Info("length fragMap   {0}", fragMap.Count);

                for (int i = 0; i < dimTagMap.Length; ++i)
                {
                    var key = fragMap[i].Item2;
                    if (!volumeMap.ContainsKey(key)) continue;

                    Logger.Info("{0} -> {1}", key, string.Join(", ", dimTagMap[i].Select(x => x.Item2)));

                    Logger.Info("Adding physical group '{0}'...", volumeMap[key]);
                    if (dimTagMap[i].Length < 1) Logger.Info("    ... no child entities!");

                    Gmsh.AddPhysicalGroup(3, dimTagMap[i].Select(x => x.Item2).ToArray(), volumeMap[key]);
                }

                Gmsh.SetNumber("Mesh.MeshSizeMin", sizeMin);
                Gmsh.SetNumber("Mesh.MeshSizeMax", sizeMax);

                Gmsh.SetNumber("Mesh.MeshSizeFromCurvature", 12);


                Gmsh.SetNumber("Mesh.SaveGroupsOfElements", -1001);
                Gmsh.SetNumber("Mesh.SaveGroupsOfNodes", 2);
                Gmsh.SetNumber("Mesh.ElementOrder", 2);
                Gmsh.SetNumber("Mesh.SecondOrderLinear", 1);
                Gmsh.SetNumber("Mesh.HighOrderOptimize", 1);

                //Gmsh.SetNumber("Mesh.Algorithm3D", 4);

                Gmsh.Generate(3);

                //Gmsh.Mesh.RemoveDuplicateNodes();
                //Gmsh.Mesh.RemoveDuplicateElements();

                var debug_inp_output_path = System.IO.Path.Combine(Settings.CalculixOutputPath, "debug.inp");
                Gmsh.Write(debug_inp_output_path);

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
                    /*if (nodeDict.ContainsKey(nodeTag))
                    {
                        var pt0 = nodeDict[nodeTag];
                        Logger.Error("existing  {0} -> {1:0.00}, {1:0.00}, {1:0.00}", nodeTag, pt0.Item1, pt0.Item2, pt0.Item3);
                        Logger.Error("new       {0} -> {1:0.00}, {1:0.00}, {1:0.00}", nodeTag, nodeCoord.Item1, nodeCoord.Item2, nodeCoord.Item3);
                        throw new Exception("Existing node.");
                    }*/
                    nodes[nodeTag] = nodeCoord;
                }

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
                    Logger.Info("Element type: {0} ({1})", elementType, elementName);
                    Logger.Info("Num elements: {0}", elementTagsIntPtr.Length);

                    Logger.Info("nElementNodeTags {0}", elementNodeTags[i].Length);
                    Logger.Info("nElementNodeTags {0}", elementNodeTags[i].Length);

                    for (int j = 0; j < elementTagsIntPtr[i].Length; ++j)
                    {
                        var elementTag = (int)elementTagsIntPtr[i][j];
                        elementTypes[elementTag] = elementType;
                        var elementNodes = new int[numNodes];
                        //Logger.Info("{0}", elementTag);

                        for (int k = 0; k < numNodes; ++k)
                        {
                            if ((int)elementNodeTags[i][j * numNodes + k] == 0) Logger.Error("Node ID can't be 0");
                            elementNodes[k] = (int)elementNodeTags[i][j * numNodes + k];
                            //Logger.Info("    {0}", elementNodes[k]);

                        }

                        elements[elementTag] = elementNodes;
                    }
                }
                //throw new Exception("Stop right now, thank you very much.");

                nodeGroups = new Dictionary<string, List<int>>();
                elementGroups = new Dictionary<string, List<Pair>>();

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
                        foreach(var ele in elementTagsIntPtr)
                            elementGroups[pgName].AddRange(ele.Select(x => new Pair(pg.Item1, (int)x)));
                    }
                }

                //Gmsh.Write(outputPath);
                
            }
            /*catch (Exception e)
            {
                Logger.Error(e.Message);
                Logger.Error(e.InnerException);
                Logger.Error(Gmsh.Logger.GetLastError());

                elementTypes = null;
                elementTags = null;
                elements = null;
                nodes = null;
                nodeTags = null;
                nodeGroups = null;
                elementGroups = null;

                throw e;
            }
                */
            Gmsh.Logger.Stop();
            Gmsh.FinalizeGmsh();

        }

        public static void BrepStepGmsh1(string filePath, IList<Brep> breps, IList<string> names, out int[] tags3d, double scale = 1.0)
        {
            Pair[] dimTags;

            var stp_options = new Rhino.FileIO.FileStpWriteOptions();
            stp_options.SplitClosedSurfaces = false;

            var doc = Rhino.RhinoDoc.CreateHeadless("");

            for (int i = breps.Count - 1; i >= 0; --i)
            {
                var attr = new Rhino.DocObjects.ObjectAttributes();
                attr.Name = names[i];
                attr.WireDensity = -1;

                var brep = breps[i].DuplicateBrep();
                brep.Transform(Transform.Scale(Point3d.Origin, scale));
                doc.Objects.AddBrep(brep, attr);
            }

            Rhino.FileIO.FileStp.Write(filePath, doc, stp_options);

            doc.Dispose();

            Gmsh.OCC.ImportShapes(filePath, out dimTags, false, "stp");

            Gmsh.OCC.Synchronize();

            dimTags = Gmsh.OCC.GetEntities(3);

            if (dimTags.Length != breps.Count) throw new Exception("Imported entity count does not match exported object count.");

            tags3d = new int[breps.Count];
            for (int i = 0; i < tags3d.Length; ++i)
            {
                tags3d[i] = dimTags[i].Item2;
            }
        }

        public static void BrepStepGmshIndividual(string tempDirectory, IList<Brep> breps, IList<string> names, out int[] tags3d, double scale = 1.0)
        {
            Pair[] dimTags;

            var stp_options = new Rhino.FileIO.FileStpWriteOptions();
            stp_options.SplitClosedSurfaces = false;


            var N = Math.Min(breps.Count, names.Count);

            tags3d = new int[N];

            for (int i = 0; i < N; ++i)
            {
                var brep = breps[i].DuplicateBrep();

                var name = string.Format("Brep{0:00}", i);
                name = names[i];

                var temp_path = System.IO.Path.Combine(tempDirectory, string.Format("{0}.stp", name));

                if (!System.IO.File.Exists(temp_path))
                {
                    brep.Transform(Transform.Scale(Point3d.Origin, scale));

                    var doc = Rhino.RhinoDoc.CreateHeadless("");
                    var attr = new Rhino.DocObjects.ObjectAttributes();
                    attr.Name = name;
                    attr.WireDensity = -1;

                    doc.Objects.AddBrep(brep, attr);

                    Rhino.FileIO.FileStp.Write(temp_path, doc, stp_options);

                    doc.Dispose();
                }

                Gmsh.OCC.ImportShapes(temp_path, out dimTags, false, "stp");
                Gmsh.OCC.Synchronize();

                foreach (var tag in dimTags)
                {
                    if (tag.Item1 == 3)
                    {
                        tags3d[i] = tag.Item2;
                    }
                }
            }
        }

    }
}
