
using RawLamAllocator;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;
using OpenNestLib;
using NLog;
using System.Diagnostics;
using System.Threading;

using Speckle.Core.Models;
using CaeGlobals;
using RawLam;
using CaeModel;
using CaeMesh;
using FileInOut;
using Sentry;
using Rhino.DocObjects;
using System.IO;
using System.Xml.Linq;

using DeepSight;

using RawLamb;
using CaeResults;
using Objects.Other;
using Rhino;
using Speckle.Core.Credentials;

namespace RawLamAllocator
{

    public class AllocatorResults : Base
    {
        public string ModelName { get; set; }
        public string FrdPath { get; set; }
        public string InpPath { get; set; }
        public string RhinoPath { get; set; }

        public double MaxDisplacement { get; set; }
        public double MinDisplacement { get; set; }

        public List<Objects.Other.Transform> ComponentTransforms { get; set; }
        public List<string> ComponentNames { get; set; }
        public List<string> ComponentBoards { get; set; }
        public List<string> ComponentLogs { get; set; }

        public AllocatorResults()
        {
            ComponentTransforms = new List<Objects.Other.Transform>();
            ComponentNames = new List<string>();
            ComponentBoards = new List<string>();
            ComponentLogs = new List<string>();
        }
    }

    internal partial class Allocator
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private static List<rlComponent> Components = new List<rlComponent>();
        private static List<RawLamb.Board> Boards = new List<RawLamb.Board>();
        private static Dictionary<string, RawLamb.Log> Logs = new Dictionary<string, RawLamb.Log>();
        private static List<Rhino.Geometry.Mesh> Supports = new List<Rhino.Geometry.Mesh>();

        private static Dictionary<string, Rhino.Geometry.Transform> Log2BoardTransforms = new Dictionary<string, Rhino.Geometry.Transform>();
        
        private static Dictionary<string, Rhino.Geometry.Transform> PlacementTransforms = new Dictionary<string, Rhino.Geometry.Transform>();
        private static Dictionary<string, Rhino.Geometry.Transform> World2LocalTransforms = new Dictionary<string, Rhino.Geometry.Transform>();
        private static Dictionary<string, Rhino.Geometry.Transform> Component2LogTransforms = new Dictionary<string, Rhino.Geometry.Transform>();
        private static Dictionary<string, string> ComponentBoardMap = new Dictionary<string, string>();
        private static Dictionary<string, string> BoardLogMap = new Dictionary<string, string>();

        private static Dictionary<string, LogModel> LogModels = new Dictionary<string, LogModel>();


        private static Settings Settings;
        private static double Scale = 0.001; // Millimetres to metres

        public static List<int> GetNodesOnMesh(RhinoDoc rhinoDocument, FeModel model, IList<Rhino.Geometry.Mesh> meshes, Step step, double tolerance = 1.0)
        {
            var lengthScale = 1.0;
            switch (model.UnitSystem.LengthUnitAbbreviation)
            {
                case ("M"):
                    lengthScale = RhinoMath.UnitScale(Rhino.UnitSystem.Meters, rhinoDocument.ModelUnitSystem);
                    break;
                case ("MM"):
                    lengthScale = RhinoMath.UnitScale(Rhino.UnitSystem.Millimeters, rhinoDocument.ModelUnitSystem);
                    break;
                default:
                    lengthScale = RhinoMath.UnitScale(Rhino.UnitSystem.Meters, rhinoDocument.ModelUnitSystem);
                    break;
            }

            var nodes = model.Mesh.Nodes;

            Point3d cp;

            var labels = new List<int>();

            foreach (Rhino.Geometry.Mesh m in meshes)
            {

                foreach (var node in nodes.Values)
                {
                    var pt = new Point3d(node.Coor[0], node.Coor[1], node.Coor[2]) * lengthScale;
                    var res = m.ClosestPoint(
                      pt,
                      out cp,
                      tolerance);

                    if (res > -1)
                    {
                        labels.Add(node.Id);
                    }
                }
            }

            return labels;
        }


            public static void Run(string settingsPath = "settings.config")
        {
            #region Setup logging
            var loggingConfig = new NLog.Config.LoggingConfiguration();

            // Targets where to log to: File and Console
            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = "main.log" };
            logfile.Layout = NLog.Layouts.Layout.FromString("${longdate}|${uppercase:${level}}|${message}");

            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");
            logconsole.Layout = NLog.Layouts.Layout.FromString("${longdate}|${uppercase:${level}}|${message}");

            // Rules for mapping loggers to targets            
            loggingConfig.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
            loggingConfig.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);

            // Apply config           
            NLog.LogManager.Configuration = loggingConfig;

            #endregion
            Logger.Info("#############################################");
            Logger.Info("#############################################");
            Logger.Info("#############################################");
            Logger.Info("");
            Logger.Info("RawLam Material Allocator");
            Logger.Info("Tom Svilans, 2023");
            Logger.Info("");

            var timer = new Stopwatch();
            timer.Start();

            Logger.Info("Started timer...");

            #region Load configuration file
            //ExeConfigurationFileMap configMap = new ExeConfigurationFileMap();
            //configMap.ExeConfigFilename = @"main.config";
            //Config = ConfigurationManager.OpenMappedExeConfiguration(configMap, ConfigurationUserLevel.None);

            Settings = Settings.Read(System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(settingsPath)));
            if (Settings == null) throw new Exception("Settings failed to load!");
            
            Logger.Info("#############################################");

            Logger.Info("Loaded configuration from {0} ", System.IO.Path.GetFullPath(@"settings.config"));
            Logger.Info("");
            Settings.Dump(Logger);

            //foreach (KeyValueConfigurationElement item in Config.AppSettings.Settings)
            //{
            //    Logger.Info("{0} : {1}", item.Key, item.Value);
            //}
            #endregion

            #region Import data from Speckle

            Logger.Info("");
            Logger.Info("#############################################");
            Logger.Info("Retrieving model...");
            Logger.Info("#############################################");
            Logger.Info("");


            var boardsLog = System.IO.File.ReadAllLines(System.IO.Path.Combine(Settings.ProjectDirectory, Settings.ModelDirectory, Settings.BoardsDatabaseName + ".log"));
            var elementsLog = System.IO.File.ReadAllLines(System.IO.Path.Combine(Settings.ProjectDirectory, Settings.ModelDirectory, Settings.ElementsDatabaseName + ".log"));

            var account = Speckle.Core.Credentials.AccountManager.GetDefaultAccount();

            Speckle.Core.Transports.ITransport boardsTransport, elementsTransport;
            switch (Settings.BoardsSource)
            {
                case ("stream"):
                    boardsTransport = new Speckle.Core.Transports.SQLiteTransport(Settings.ProjectDirectory, Settings.ModelDirectory, Settings.BoardsDatabaseName);
                    boardsTransport = new Speckle.Core.Transports.ServerTransport(account, Settings.BoardsStreamId);

                    break;
                default:
                    boardsTransport = new Speckle.Core.Transports.SQLiteTransport(Settings.ProjectDirectory, Settings.ModelDirectory, Settings.BoardsDatabaseName);
                    Logger.Info("Established board transport at {0}", (boardsTransport as Speckle.Core.Transports.SQLiteTransport).RootPath);

                    break;
            }

            switch (Settings.ElementsSource)
            {
                case ("stream"):
                    //elementsTransport = new Speckle.Core.Transports.SQLiteTransport(Settings.ProjectDirectory, Settings.ModelDirectory, Settings.ElementsDatabaseName);
                    elementsTransport = new Speckle.Core.Transports.ServerTransport(account, Settings.ElementsStreamId);
                    break;
                default:
                    elementsTransport = new Speckle.Core.Transports.SQLiteTransport(Settings.ProjectDirectory, Settings.ModelDirectory, Settings.ElementsDatabaseName);
                    Logger.Info("Established elements transport at {0}", (elementsTransport as Speckle.Core.Transports.SQLiteTransport).RootPath);

                    break;
            }

            var resultsTransport = new Speckle.Core.Transports.SQLiteTransport(Settings.ProjectDirectory, Settings.ModelDirectory, "results");


            var boardsCommit = boardsLog[boardsLog.Length - 1].Trim();
            var elementsCommit = elementsLog[elementsLog.Length - 1].Trim();

            Speckle.Core.Models.Base boardsObj = null, elementsObj = null;
            try
            {
                Logger.Info("Attempting to fetch commit '{0}'...", boardsCommit);
                boardsObj = Task.Run(async () => await Speckle.Core.Api.Operations.Receive(boardsCommit, null, boardsTransport, disposeTransports: true)).Result;
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
            try
            {
                Logger.Info("Attempting to fetch commit '{0}'...", elementsCommit);
                elementsObj = Task.Run(async () => await Speckle.Core.Api.Operations.Receive(elementsCommit, null, elementsTransport, disposeTransports: true)).Result;
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }

            Logger.Info(boardsObj);
            Logger.Info(elementsObj);

            Logger.Info("");
            Logger.Info("#############################################");
            Logger.Info("Iterating received logs...");
            Logger.Info("#############################################");
            Logger.Info("");

            var converter = new Objects.Converter.RhinoGh.ConverterRhinoGh();
            var rhinoDoc = Rhino.RhinoDoc.CreateHeadless("");
            rhinoDoc.ModelUnitSystem = Rhino.UnitSystem.Millimeters;
            converter.SetContextDocument(rhinoDoc);

            var board_meshes = new List<Rhino.Geometry.Mesh>();

            if (boardsObj != null)
            {
                var speckleLogs = boardsObj["logs"] as List<object>;
                foreach (Base speckleLog in speckleLogs)
                {
                    var logName = speckleLog["name"] as string;
                    Logger.Info(logName);

                    var infologPath = speckleLog["infolog_path"] as string;
                    var gridPath = speckleLog["grid_path"] as string;

                    var log = new RawLamb.Log();
                    log.Name = logName;
                    log.ReadInfoLog(infologPath);


                    var loadedGrids = GridIO.Read(gridPath);

                    if (loadedGrids.Length > 0)
                        log.Grids["density"] = loadedGrids[0] as FloatGrid;

                    var logPlane = converter.PlaneToNative(speckleLog["plane"] as Objects.Geometry.Plane);
                    log.Plane = logPlane;


                    Logger.Info("    {0:0.000}", logPlane);
                    Logger.Info("    {0}", speckleLog["grid_path"]);
                    Logger.Info("    {0}", speckleLog["infolog_path"]);

                    var inputBoards = speckleLog["boards"] as List<object>;
                    foreach (Base speckleBoard in inputBoards)
                    {
                        Logger.Info("      {0} numTop {1} numBottom {2}", speckleBoard["name"],
                            (speckleBoard["outline_top"] as List<object>).Count,
                            (speckleBoard["outline_bottom"] as List<object>).Count);
                        Logger.Info("        {0:0.000}", speckleBoard["plane"]);

                        var board = new RawLamb.Board();
                        board.Name = speckleBoard["name"] as string;
                        board.Plane = converter.PlaneToNative(speckleBoard["plane"] as Objects.Geometry.Plane);

                        board_meshes.Add(converter.MeshToNative(speckleBoard["mesh"] as Objects.Geometry.Mesh) );

                        foreach (Objects.Geometry.Polyline outline in speckleBoard["outline_top"] as List<object>)
                        {
                            var poly = converter.PolylineToNative(outline).ToPolyline();
                            if (poly == null) Logger.Error("Failed to get polyline.");
                            board.Top.Add(poly);
                        }

                        foreach (Objects.Geometry.Polyline outline in speckleBoard["outline_bottom"] as List<object>)
                        {
                            var poly = converter.PolylineToNative(outline).ToPolyline();
                            if (poly == null) Logger.Error("Failed to get polyline.");
                            board.Bottom.Add(poly);
                        }

                        board.Log = log;
                        log.Boards.Add(board);

                        BoardLogMap[board.Name] = log.Name;

                        Boards.Add(board);

                    }

                    Logs[log.Name] = log;
                    LogModels[log.Name] = new SimpleLogModel(Rhino.Geometry.Plane.WorldXY, 4200, 200, 200, 0.05, 0);
                }
            }

            Logger.Info("");
            Logger.Info("#############################################");
            Logger.Info("Iterating received elements...");
            Logger.Info("#############################################");
            Logger.Info("");

            var speckleElements = elementsObj["elements"] as List<object>;

            if (elementsObj.GetDynamicMemberNames().Contains("supports"))
            {
                Logger.Info("Found supports.");
                var speckleSupports = elementsObj["supports"] as List<object>;
                foreach (Objects.Geometry.Mesh speckleSupport in speckleSupports)
                {
                    Supports.Add(converter.MeshToNative(speckleSupport));
                }

                Logger.Info("Added {0} support meshes.", Supports.Count);
            }

            var maxCount = Settings.DebugMaxElements;
            int counter = 0;
            foreach (Base element in speckleElements)
            {
                var notes = new List<string>();
                var brep = converter.BrepToNative(element["geometry"] as Objects.Geometry.Brep, out notes);
                var baseplane = converter.PlaneToNative(element["baseplane"] as Objects.Geometry.Plane);
                var componentName = element["name"] as string;
                Logger.Info("{0}    {1:0.000}    is solid: {2}", componentName, baseplane, brep.IsSolid);

                Components.Add(new rlComponent(baseplane, brep, componentName));

                World2LocalTransforms[componentName] = new Rhino.Geometry.Transform(Rhino.Geometry.Transform.PlaneToPlane(baseplane, Rhino.Geometry.Plane.WorldXY));

                var attributes = new Rhino.DocObjects.ObjectAttributes();
                attributes.Name = componentName;
                rhinoDoc.Objects.AddBrep(brep, attributes);
                counter++;
                if (counter > maxCount) break;
            }

            #endregion

            #region Construct FE model
            Logger.Info("");
            Logger.Info("#############################################");
            Logger.Info("Constructing FE model...");
            Logger.Info("#############################################");
            Logger.Info("");

            Dictionary<int, double[]> nodes;
            Dictionary<int, int[]> elements;
            Dictionary<int, int> elementTypes;

            Dictionary<string, List<int>> nodeGroups;
            Dictionary<string, List<Tuple<int, int>>> elementGroups;

            double size_min = Settings.FeMeshSizeMin;
            double size_max = Settings.FeMeshSizeMax;

            DoMeshing(rhinoDoc, Components.Select(x => x.Geometry).ToList(), Components.Select(x => x.Name).ToList(), 
                out nodes, out elements, out nodeGroups, out elementGroups, out elementTypes, size_min, size_max);

            var model = new FeModel("EmaObservatory");
            model.Properties.ModelSpace = ModelSpaceEnum.ThreeD;
            model.Properties.ModelType = ModelType.GeneralModel;
            model.UnitSystem = new CaeGlobals.UnitSystem(UnitSystemType.M_KG_S_C);

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

            var supportNodes = GetNodesOnMesh(rhinoDoc, model, Supports, step, 0.001);
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

            #endregion

            
            #region Offset, simplify, and get largest board outlines
            Logger.Info("");
            Logger.Info("#############################################");
            Logger.Info("Offsetting and simplifying board outlines...");
            Logger.Info("#############################################");
            Logger.Info("");

            var simpleBoardOutlines = new List<Polyline>();

            foreach (var board in Boards)
            {
                var allOutlines = new List<Polyline>();
                allOutlines.AddRange(board.Top);
                allOutlines.AddRange(board.Bottom);

                for (int i = 0; i < allOutlines.Count; ++i)
                    allOutlines[i].Transform(Rhino.Geometry.Transform.PlaneToPlane(board.Plane, Rhino.Geometry.Plane.WorldXY));

                var simpleOutline = rlComponent.UnionOffset(allOutlines, Settings.BoardOffset);


                simpleOutline.ReduceSegments(1.0);
                simpleOutline.CollapseShortSegments(30.0);

                // Orient board from global to local space

                simpleBoardOutlines.Add(simpleOutline);
                //rhinoDoc.Objects.AddPolyline(simpleOutline);


                Logger.Info("Processed board {0}", board.Name);
            }

            //rhinoDoc.Write3dmFile(System.IO.Path.Combine(projectDir, modelDir, "temp.3dm"), new Rhino.FileIO.FileWriteOptions());
            //return;

            #endregion

            // Orient boards to WorldXY (World -> Local)

            #region Get element outlines
            Logger.Info("");
            Logger.Info("#############################################");
            Logger.Info("Generating element outlines...");
            Logger.Info("#############################################");
            Logger.Info("");


            foreach (var component in Components)
            {
                var mesh_parameters = new Rhino.Geometry.MeshingParameters(0.5);
                var mesh = new Rhino.Geometry.Mesh();

                var res = Rhino.Geometry.Mesh.CreateFromBrep(component.Geometry, mesh_parameters);
                foreach (var r in res)
                    mesh.Append(r);

                if (!mesh.IsValid)
                {
                    Logger.Error("Failed to generate mesh for component '{0}'.", component.Name);
                }

                Logger.Info("Created mesh with {0} vertices and {1} faces.", mesh.Vertices.Count, mesh.Faces.Count);

                mesh.Transform(Rhino.Geometry.Transform.PlaneToPlane(component.Plane, Rhino.Geometry.Plane.WorldXY));
                var outline = rlComponent.MeshOutline(mesh);
                
                if (outline == null || outline.Length < 1) Logger.Error("MeshOutline for component '{0}' failed.", component.Name);

                component.Outline = outline;
            }


            var testElements = new List<Polyline>();

            foreach (var component in Components)
            {
                if (component.Outline.Length < 1)
                {
                    Logger.Error("Component '{0}' contains no outlines.", component.Name);
                    continue;
                }

                var firstOutline = component.Outline[0].Duplicate();

                // Simplify outline
                firstOutline.ReduceSegments(5.0);
                firstOutline.CollapseShortSegments(10.0);

                testElements.Add(firstOutline);
            }
            #endregion

            Logger.Info("Generating sheets...");
            var sheets = simpleBoardOutlines;

            // Get element mesh outlines and orient to WorldXY (World -> Local)
            var GlobalRandom = new Random((int)DateTime.Now.Ticks);
            var ShuffleSeed = GlobalRandom.Next(int.MaxValue);
            var NestingSeed = GlobalRandom.Next(int.MaxValue);

            Logger.Info("Model construction time: {0}", timer.Elapsed.ToString(@"mm\:ss\.fff"));

            Logger.Info("");
            Logger.Info("##########################################################################################");
            Logger.Info("START ITERATION -> shuffle boards, change placement seed");
            Logger.Info("##########################################################################################");
            Logger.Info("");

            bool kill = false;
            int totalIterations = 0;
            double currentMax = double.NaN, currentMin = double.NaN;

            while (!kill)
            {
                timer.Restart();
                Logger.Info("");
                Logger.Info("##########################################################################################");
                Logger.Info("ITERATION {0} -> last min: {1}, last max: {2}", totalIterations, currentMin, currentMax);
                Logger.Info("##########################################################################################");
                Logger.Info("");

                ShuffleSeed = GlobalRandom.Next(int.MaxValue);
                NestingSeed = GlobalRandom.Next(int.MaxValue);

                Logger.Info("Shuffle seed: {0}", ShuffleSeed);
                Logger.Info("Nesting seed: {0}", NestingSeed);

                var zipped = new List<Tuple<string, Polyline, int>>();
                var nElements = Boards.Count;

                for (int i = 0; i < Boards.Count; ++i)
                {
                    zipped.Add(new Tuple<string, Polyline, int>(Boards[i].Name, sheets[i].Duplicate(), i));
                }

                Random rng = new Random(ShuffleSeed);

                while (nElements > 1)
                {
                    nElements--;
                    int k = rng.Next(nElements + 1);
                    var v = zipped[k];
                    zipped[k] = zipped[nElements];
                    zipped[nElements] = v;
                }

                var shuffledSheets = zipped.Select(x => x.Item2).ToList();
                var shuffleMap = zipped.Select(x => x.Item3).ToList();


                #region Do the OpenNest thing
                Logger.Info("");
                Logger.Info("#############################################");
                Logger.Info("Allocate and nest outlines...");
                Logger.Info("#############################################");
                Logger.Info("");

                if (shuffledSheets.Count > 0)
                {

                    Rhino.Geometry.Transform[] transforms;
                    int[] sheetIds;

                    Logger.Info("Performing nesting...");
                    Logger.Info("Number of elements: {0}", testElements.Count);
                    Logger.Info("Number of boards:   {0}", shuffledSheets.Count);
                    var fitness = PerformNesting(NestingSeed, testElements, shuffledSheets, out transforms, out sheetIds, 1);
                    if (fitness == double.NaN)
                    {
                        Logger.Error("Nesting failed and returned garbage. Trying again...");
                        Logger.Error("");
                        totalIterations++;
                        continue; // If the nesting fucks up, skip the whole iteration
                    }

                    Logger.Info("Nesting time {0}", timer.Elapsed.ToString(@"mm\:ss\.fff"));

                    var sheetMap = new Dictionary<int, List<int>>();

                    for (int i = 0; i < sheetIds.Length; ++i)
                    {
                        var realId = shuffleMap[sheetIds[i]];
                        if (!sheetMap.ContainsKey(realId))
                            sheetMap[realId] = new List<int>();
                        sheetMap[realId].Add(i);
                    }

                    Random rand = new Random();

                    foreach (var kvp in sheetMap)
                    {
                        var ids = new List<Guid>();
                        var layer = new Rhino.DocObjects.Layer();

                        var board = Boards[kvp.Key];
                        layer.Name = board.Name;
                        layer.Color = System.Drawing.Color.FromArgb(rand.Next(155) + 100, rand.Next(155) + 100, rand.Next(155) + 100);

                        var layerIndex = rhinoDoc.Layers.Add(layer);

                        var board2logWorld = Rhino.Geometry.Transform.PlaneToPlane(Rhino.Geometry.Plane.WorldXY, board.Plane);
                        var logWorld2logLocal = Rhino.Geometry.Transform.PlaneToPlane(board.Log.Plane, Rhino.Geometry.Plane.WorldXY);

                        var attributes = new Rhino.DocObjects.ObjectAttributes();
                        attributes.LayerIndex = layerIndex;

                        var boardOutline = sheets[kvp.Key].Duplicate();
                        boardOutline.Transform(board2logWorld);
                        boardOutline.Transform(logWorld2logLocal);

                        ids.Add(rhinoDoc.Objects.AddPolyline(boardOutline, attributes));
                        foreach (var ele in kvp.Value)
                        {
                            if (!transforms[ele].IsValid)
                            {
                                Logger.Error("Transform for component '{0}' is not valid: {1}", Components[ele].Name, transforms[ele]);
                                //continue;
                            }

                            var element = testElements[ele].Duplicate();
                            element.Transform(transforms[ele]);
                            element.Transform(board2logWorld);
                            element.Transform(logWorld2logLocal);

                            var component2logTransform = logWorld2logLocal * board2logWorld * transforms[ele] * Rhino.Geometry.Transform.PlaneToPlane(Components[ele].Plane, Rhino.Geometry.Plane.WorldXY);

                            var geo = Components[ele].Geometry.DuplicateBrep();

                            geo.Transform(component2logTransform);

                            attributes.Name = Components[ele].Name;

                            Component2LogTransforms[Components[ele].Name] = component2logTransform;
                            PlacementTransforms[Components[ele].Name] = transforms[ele];
                            ComponentBoardMap[Components[ele].Name] = board.Name;

                            ids.Add(rhinoDoc.Objects.AddPolyline(element, attributes));
                            ids.Add(rhinoDoc.Objects.AddBrep(geo, attributes));
                        }

                        //rhinoDoc.Groups.Add(boards[kvp.Key].Name, ids);
                    }
                }
                #endregion

                #region Set material orientations in FE model

                model.Mesh.ElementOrientations.Clear();

                foreach (var component in Components)
                {
                    Rhino.Geometry.Transform xform = Component2LogTransforms[component.Name];
                    Rhino.Geometry.Transform inv;
                    var board = Boards.Where(x => x.Name == ComponentBoardMap[component.Name]).First() as RawLamb.Board;
                    var log = board.Log;

                    xform.TryGetInverse(out inv);
                    if (model.Mesh.ElementSets.ContainsKey(component.Name))
                    {
                        Logger.Info("Generating distributions for component '{0}'...", component.Name);

                        var elset = model.Mesh.ElementSets[component.Name];

                        var samplePoints = new Point3d[elset.Labels.Length];
                        var elementOrientations = new Rhino.Geometry.Plane[elset.Labels.Length];
                        for (int i = 0; i < elset.Labels.Length; ++i)
                        {
                            var elementId = elset.Labels[i];
                            var cg = model.Mesh.Elements[elementId].GetCG(model.Mesh.Nodes);
                            var samplePoint = new Point3d(cg[0], cg[1], cg[2]) * 1 / Scale;
                            samplePoint.Transform(xform);
                            samplePoints[i] = samplePoint;
                        }

                        log.GetMaterialOrientations(LogModels[log.Name], new FlowlineKnotFibreOrientationModel(), samplePoints, out elementOrientations);

                        var debugIds = new List<Guid>();
                        var xattributes = new Rhino.DocObjects.ObjectAttributes { ObjectColor = System.Drawing.Color.Red, ColorSource = ObjectColorSource.ColorFromObject };
                        var yattributes = new Rhino.DocObjects.ObjectAttributes { ObjectColor = System.Drawing.Color.SpringGreen, ColorSource = ObjectColorSource.ColorFromObject };

                        for (int i = 0; i < elset.Labels.Length; ++i)
                        {
                            var elementId = elset.Labels[i];
                            var elementOrientation = elementOrientations[i];

                            //debugIds.Add(rhinoDoc.Objects.AddLine(new Line(elementOrientation.Origin, elementOrientation.XAxis, 20), xattributes));
                            //debugIds.Add(rhinoDoc.Objects.AddLine(new Line(elementOrientation.Origin, elementOrientation.YAxis, 20), yattributes));

                            elementOrientation.Transform(inv);

                            if (!elementOrientation.IsValid)
                                continue;

                            try
                            {
                                model.Mesh.ElementOrientations.Add(elementId,
                                    new FeMaterialOrientation(elementId,
                                    elementOrientation.XAxis.X, elementOrientation.XAxis.Y, elementOrientation.XAxis.Z,
                                    elementOrientation.YAxis.X, elementOrientation.YAxis.Y, elementOrientation.YAxis.Z));
                            }
                            catch (Exception e)
                            {
                                Logger.Error("Orientation for element {0} already exists.", elementId);
                                Logger.Error(e);
                            }
                        }

                        rhinoDoc.Groups.Add(component.Name, debugIds);
                    }
                }

                distribution.Labels = model.Mesh.ElementOrientations.Select(x => x.Key).ToArray();

                model.Name = string.Format("EMA{0}", DateTime.UtcNow.ToString("yyMMddHHmmss"));

                // Write input file
                var outputDirectory = Settings.CalculixOutputPath;
                outputDirectory = System.IO.Path.GetFullPath(outputDirectory);
                var inp_path = System.IO.Path.Combine(outputDirectory, model.Name + ".inp");
                Logger.Info("Writing INP file: {0}", inp_path);
                FileInOut.Output.CalculixFileWriter.Write(inp_path, model);

                Logger.Info("Saving debug Rhino file...");
                var rhino_path = System.IO.Path.Combine(Settings.CalculixOutputPath, model.Name + ".3dm");
                var docViews = rhinoDoc.Views.GetStandardRhinoViews();
                foreach (var docView in docViews)
                {
                    docView.MainViewport.ZoomExtents();
                    docView.MainViewport.DisplayMode = Rhino.Display.DisplayModeDescription.GetDisplayMode(Rhino.Display.DisplayModeDescription.ShadedId);
                }
                rhinoDoc.Write3dmFile(rhino_path, new Rhino.FileIO.FileWriteOptions());
                
                rhinoDoc.Objects.Clear();
                rhinoDoc.Groups.Clear();
                rhinoDoc.Layers.Clear();

                #endregion


                #region Launch CalculiX

                Logger.Info("");
                Logger.Info("#############################################");
                Logger.Info("Run CalculiX...");
                Logger.Info("#############################################");
                Logger.Info("");

                var ccx_path = Settings.CalculixExePath;

                if (!System.IO.Directory.Exists(outputDirectory))
                    System.IO.Directory.CreateDirectory(outputDirectory);

                Logger.Info("CCX output directory : {0}", outputDirectory);

                var ccx_args = string.Format(" -i \"{0}\"", System.IO.Path.Combine(outputDirectory, model.Name));

                LaunchCCX(ccx_path, ccx_args, outputDirectory);

                #endregion

                Logger.Info("");
                Logger.Info("#############################################");
                Logger.Info("Evaluating CalculiX simulation results...");
                Logger.Info("#############################################");
                Logger.Info("");
                // Open .frd file
                // Find maximum displacement
                // Save in Speckle results database along with element placements on boards

                var frd_path = System.IO.Path.Combine(outputDirectory, model.Name + ".frd");

                FeResults results = null;
                try
                {
                    results = FrdFileReader.Read(frd_path);

                    var field_name = "U";
                    var component_name = "ALL";
                    if (!results.GetAllFieldNames().Contains(field_name))
                        field_name = results.GetAllFieldNames().FirstOrDefault();
                    if (field_name == null)
                    {
                        Logger.Error("Couldn't get any field names. Results are probably corrupt or job failed.");
                    }
                    else
                    {

                        if (!results.GetAllFiledNameComponentNames()[field_name].Contains(component_name))
                            component_name = results.GetAllFiledNameComponentNames()[field_name].FirstOrDefault();

                        var fieldData = results.GetFieldData(field_name, component_name, results.GetAllStepIds()[0], 1);

                        var values = results.GetValues(fieldData, results.Mesh.Nodes.Keys.ToArray());

                        currentMin = values.Min();
                        currentMax = values.Max();

                        // Compose results object

                        var allocationResults = new AllocatorResults
                        {
                            ModelName = model.Name,
                            FrdPath = frd_path,
                            InpPath = inp_path,
                            RhinoPath = rhino_path,
                            MinDisplacement = currentMin,
                            MaxDisplacement = currentMax
                        };

                        for (int i = 0; i < Components.Count; ++i)
                        {
                            allocationResults.ComponentNames.Add(Components[i].Name);
                            allocationResults.ComponentTransforms.Add(new Objects.Other.Transform(Component2LogTransforms[Components[i].Name].ToFloatArray(true)));
                            allocationResults.ComponentBoards.Add(ComponentBoardMap[Components[i].Name]);
                            allocationResults.ComponentLogs.Add(BoardLogMap[ComponentBoardMap[Components[i].Name]]);
                        }

                        // Send results object to database
                        var resString = Task.Run(async () => await Speckle.Core.Api.Operations.Send(allocationResults, new List<Speckle.Core.Transports.ITransport> { resultsTransport }, false)).Result;
                        // Write to simulation commit file
                        var resultsLogPath = System.IO.Path.Combine(Settings.ProjectDirectory, Settings.ModelDirectory, "results.log");
                        System.IO.File.AppendAllLines(resultsLogPath, new string[] { resString });
                    }
                }
                catch(Exception e)
                {
                    Logger.Error(e);
                    continue;
                }

                // Increment iteration counter and go again...
                totalIterations++;
                if (totalIterations > Settings.MaxIterations) kill = true;
            }

            Logger.Info("");
            Logger.Info("##########################################################################################");
            Logger.Info("END ITERATION -> save board placements and maximum displacement from .FRD");
            Logger.Info("##########################################################################################");
            Logger.Info("");
            Logger.Info("Last iteration time: {0}", timer.Elapsed.ToString(@"mm\:ss\.fff"));
            Logger.Info("");




            Logger.Info("All done. Have a nice day.");
            Console.WriteLine("press any key to exit");
            Console.ReadKey();

            #region Clean-up

            NLog.LogManager.Shutdown(); // Flush and close down internal threads and timers

            rhinoDoc.Dispose();

            #endregion
        }

        public static void LaunchCCX(string _executable, string _argument, string _workDirectory)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.CreateNoWindow = true;
            psi.FileName = _executable;
            psi.Arguments = _argument;
            psi.WorkingDirectory = _workDirectory;
            psi.WindowStyle = ProcessWindowStyle.Normal;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            psi.EnvironmentVariables["OMP_NUM_THREADS"] = "8";
            psi.EnvironmentVariables["CCX_NPROC_STIFFNESS"] = "8";
            psi.EnvironmentVariables["NUMBER_OF_CPUS"] = "8";

            var _exe = new Process();
            _exe.StartInfo = psi;
            //
            using (AutoResetEvent outputWaitHandle = new AutoResetEvent(false))
            using (AutoResetEvent errorWaitHandle = new AutoResetEvent(false))
            {
                _exe.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data == null)
                    {
                        // the safe wait handle closes on kill
                        if (!outputWaitHandle.SafeWaitHandle.IsClosed) outputWaitHandle.Set();
                    }
                    else
                    {
                        Logger.Info(e.Data);
                    }
                };
                //
                _exe.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data == null)
                    {
                        // the safe wait handle closes on kill
                        if (!errorWaitHandle.SafeWaitHandle.IsClosed) errorWaitHandle.Set();
                    }
                    else
                    {
                        //File.AppendAllText(_errorFileName, e.Data + Environment.NewLine);
                        Logger.Info(e.Data);
                    }
                };
                //
                _exe.Start();
                //
                _exe.BeginOutputReadLine();
                _exe.BeginErrorReadLine();
                int ms = 1000 * 3600 * 24 * 7 * 3; // 3 weeks
                if (_exe.WaitForExit(ms) && outputWaitHandle.WaitOne(ms) && errorWaitHandle.WaitOne(ms))
                {
                    // Process completed. Check process.ExitCode here.
                    // after Kill() _jobStatus is Killed
                    //if (_jobStatus != JobStatus.Killed) _jobStatus = CaeJob.JobStatus.OK;
                }
                _exe.Close();
            }
        }

    }

}
