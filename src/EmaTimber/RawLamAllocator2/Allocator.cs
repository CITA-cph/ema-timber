
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Rhino.Geometry;
using Rhino.DocObjects;

using NLog;

using CaeModel;
using CaeMesh;

using RawLamb;
using Rhino;
using FileInOut.Input;
using Rhino.Input.Custom;
using System.Windows.Forms;
using CaeResults;

namespace RawLamAllocator
{



    internal partial class Allocator
    {
        internal static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public List<rlComponent> Components = new List<rlComponent>();
        public List<RawLamb.Board> Boards = new List<RawLamb.Board>();
        public Dictionary<string, RawLamb.Log> Logs = new Dictionary<string, RawLamb.Log>();
        public List<Rhino.Geometry.Mesh> Supports = new List<Rhino.Geometry.Mesh>();

        public Dictionary<string, Rhino.Geometry.Transform> Log2BoardTransforms = new Dictionary<string, Rhino.Geometry.Transform>();

        public Dictionary<string, Rhino.Geometry.Transform> PlacementTransforms = new Dictionary<string, Rhino.Geometry.Transform>();
        public Dictionary<string, Rhino.Geometry.Transform> World2LocalTransforms = new Dictionary<string, Rhino.Geometry.Transform>();
        public Dictionary<string, Rhino.Geometry.Transform> Component2LogTransforms = new Dictionary<string, Rhino.Geometry.Transform>();
        public Dictionary<string, string> ComponentBoardMap = new Dictionary<string, string>();
        public Dictionary<string, string> BoardLogMap = new Dictionary<string, string>();

        public Dictionary<string, LogModel> LogModels = new Dictionary<string, LogModel>();

        // List of elements and boards to ignore
        public List<string> ElementIgnore = new List<string>();
        public List<string> BoardIgnore = new List<string>();

        public bool ValidMesh { get; private set; } = false;


        public Settings Settings;
        public double Scale = 0.001; // Millimetres to metres

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


        public void Run(string settingsPath = "settings.config")
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
            Logger.Info("Loading model...");
            Logger.Info("#############################################");
            Logger.Info("");

            var rhinoDoc = Rhino.RhinoDoc.CreateHeadless("");
            LoadModel(rhinoDoc);

            if (Components.Count < 1)
            {
                Logger.Fatal("No components loaded!");
                Console.ReadKey();
                return;
            }
            if (Boards.Count < 1)
            {
                Logger.Fatal("No boards loaded!");
                Console.ReadKey();
                return;
            }

            #endregion

            #region Construct FE model
            Logger.Info("");
            Logger.Info("#############################################");
            Logger.Info("Constructing FE model...");
            Logger.Info("#############################################");
            Logger.Info("");

            var model = ConstructFeModel(rhinoDoc);

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
                allOutlines.AddRange(board.Top.Select(x => x.Duplicate()));
                allOutlines.AddRange(board.Bottom.Select(x => x.Duplicate()));

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
                    var nester = new Nesting(this);
                    var fitness = nester.Run(NestingSeed, testElements, shuffledSheets, out transforms, out sheetIds, 1);
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
                        var board = Boards[kvp.Key];

                        var ids = new List<Guid>();
                        var layer = new Rhino.DocObjects.Layer();

                        var rhinoBoard = RhinoDoc.CreateHeadless("");

                        var layerBoard = new Layer();
                        layerBoard.Name = "Board";
                        var layerBoardIndex = rhinoBoard.Layers.Add(layerBoard);

                        var layerPlate = new Layer();
                        layerPlate.Name = "Plate";
                        var layerPlateIndex = rhinoBoard.Layers.Add(layerPlate);

                        var layerBoardTop = new Layer();
                        layerBoardTop.Name = "BoardOutlineTop";
                        layerBoardTop.Color = System.Drawing.Color.Red;
                        layerBoardTop.ParentLayerId = rhinoBoard.Layers.FindIndex(layerBoardIndex).Id;
                        var layerBoardTopIndex = rhinoBoard.Layers.Add(layerBoardTop);

                        var layerBoardBottom = new Layer();
                        layerBoardBottom.Name = "BoardOutlineBottom";
                        layerBoardBottom.Color = System.Drawing.Color.Blue;
                        layerBoardBottom.ParentLayerId = rhinoBoard.Layers.FindIndex(layerBoardIndex).Id;
                        var layerBoardBottomIndex = rhinoBoard.Layers.Add(layerBoardBottom);

                        var layerComponents = new Layer();
                        layerComponents.Name = "Components";
                        var layerComponentsIndex = rhinoBoard.Layers.Add(layerComponents);

                        var layerTopOutline = new Layer();
                        layerTopOutline.Name = "TopOutline";
                        layerTopOutline.Color = System.Drawing.Color.Red;
                        layerTopOutline.ParentLayerId = rhinoBoard.Layers.FindIndex(layerComponentsIndex).Id;
                        var layerTopOutlineIndex = rhinoBoard.Layers.Add(layerTopOutline);

                        var layerTopOutlineInner = new Layer();
                        layerTopOutlineInner.Name = "TopOutlineInner";
                        layerTopOutlineInner.Color = System.Drawing.Color.Red;
                        layerTopOutlineInner.ParentLayerId = rhinoBoard.Layers.FindIndex(layerComponentsIndex).Id;
                        var layerTopOutlineInnerIndex = rhinoBoard.Layers.Add(layerTopOutlineInner);

                        var layerBottomOutline = new Layer();
                        layerBottomOutline.Name = "BottomOutline";
                        layerBottomOutline.Color = System.Drawing.Color.Lime;
                        layerBottomOutline.ParentLayerId = rhinoBoard.Layers.FindIndex(layerComponentsIndex).Id;
                        var layerBottomOutlineIndex = rhinoBoard.Layers.Add(layerBottomOutline);

                        var layerBottomOutlineInner = new Layer();
                        layerBottomOutlineInner.Name = "BottomOutlineInner";
                        layerBottomOutlineInner.Color = System.Drawing.Color.Lime;
                        layerBottomOutlineInner.ParentLayerId = rhinoBoard.Layers.FindIndex(layerComponentsIndex).Id;
                        var layerBottomOutlineInnerIndex = rhinoBoard.Layers.Add(layerBottomOutlineInner);

                        var layerBrep = new Layer();
                        layerBrep.Name = "Solids";
                        layerBrep.Color = System.Drawing.Color.Black;
                        layerBrep.ParentLayerId = rhinoBoard.Layers.FindIndex(layerComponentsIndex).Id;
                        var layerBrepIndex = rhinoBoard.Layers.Add(layerBrep);

                        var layerDrill6mm = new Layer();
                        layerDrill6mm.Name = "Drill 6mm";
                        layerDrill6mm.Color = System.Drawing.Color.Pink;
                        layerDrill6mm.ParentLayerId = rhinoBoard.Layers.FindIndex(layerComponentsIndex).Id;
                        var layerDrill6mmIndex = rhinoBoard.Layers.Add(layerDrill6mm);

                        var layerDrill12mm = new Layer();
                        layerDrill12mm.Name = "Drill 12mm";
                        layerDrill12mm.Color = System.Drawing.Color.Purple;
                        layerDrill12mm.ParentLayerId = rhinoBoard.Layers.FindIndex(layerComponentsIndex).Id;
                        var layerDrill12mmIndex = rhinoBoard.Layers.Add(layerDrill12mm);

                        var layerSurfaces = new Layer();
                        layerSurfaces.Name = "Surfaces";
                        layerSurfaces.Color = System.Drawing.Color.Orange;
                        layerSurfaces.ParentLayerId = rhinoBoard.Layers.FindIndex(layerComponentsIndex).Id;
                        var layerSurfacesIndex = rhinoBoard.Layers.Add(layerSurfaces);

                        var board2logWorld = Rhino.Geometry.Transform.PlaneToPlane(Rhino.Geometry.Plane.WorldXY, board.Plane);
                        var logWorld2logLocal = Rhino.Geometry.Transform.PlaneToPlane(board.Log.Plane, Rhino.Geometry.Plane.WorldXY);

                        var boardOutline = sheets[kvp.Key].Duplicate();

                        if (Settings.DebugLogSpace > 0)
                        {
                            boardOutline.Transform(board2logWorld);
                            boardOutline.Transform(logWorld2logLocal);
                        }

                        rhinoBoard.Objects.AddRectangle(new Rectangle3d(Rhino.Geometry.Plane.WorldXY, 4880, 400), new ObjectAttributes { Name = "Plate", LayerIndex = layerPlateIndex });

                        //ids.Add(rhinoBoard.Objects.AddPolyline(boardOutline, new ObjectAttributes { LayerIndex = layerBoardTopIndex }));
                        Rhino.Geometry.Plane topPlane = Rhino.Geometry.Plane.Unset, bottomPlane = Rhino.Geometry.Plane.Unset;

                        foreach (var pl in board.Top)
                        {
                            var outline = pl.Duplicate();
                            outline.Transform(Rhino.Geometry.Transform.PlaneToPlane(board.Plane, Rhino.Geometry.Plane.WorldXY));

                            if (Settings.DebugLogSpace > 0)
                            {
                                outline.Transform(board2logWorld);
                                outline.Transform(logWorld2logLocal);
                            }

                            if (!topPlane.IsValid)
                            {
                                outline.ToNurbsCurve().TryGetPlane(out topPlane);
                            }

                            rhinoBoard.Objects.AddPolyline(outline, new ObjectAttributes { Name=board.Name, LayerIndex = layerBoardTopIndex });
                        }

                        foreach (var pl in board.Bottom)
                        {
                            var outline = pl.Duplicate();
                            outline.Transform(Rhino.Geometry.Transform.PlaneToPlane(board.Plane, Rhino.Geometry.Plane.WorldXY));

                            if (Settings.DebugLogSpace > 0)
                            {
                                outline.Transform(board2logWorld);
                                outline.Transform(logWorld2logLocal);
                            }

                            if (!bottomPlane.IsValid)
                            {
                                outline.ToNurbsCurve().TryGetPlane(out bottomPlane);
                            }


                            rhinoBoard.Objects.AddPolyline(outline, new ObjectAttributes { Name = board.Name, LayerIndex = layerBoardBottomIndex });
                        }

                        foreach (var ele in kvp.Value)
                        {
                            if (!transforms[ele].IsValid)
                            {
                                Logger.Error("Transform for component '{0}' is not valid: {1}", Components[ele].Name, transforms[ele]);
                                //continue;
                            }

                            var component2logTransform = logWorld2logLocal * board2logWorld * transforms[ele] * Rhino.Geometry.Transform.PlaneToPlane(Components[ele].Plane, Rhino.Geometry.Plane.WorldXY);
                            var component2boardTransform = transforms[ele] * Rhino.Geometry.Transform.PlaneToPlane(Components[ele].Plane, Rhino.Geometry.Plane.WorldXY);

                            var geo = Components[ele].Geometry.DuplicateBrep();

                            var plane = Components[ele].Plane;

                            if (Settings.DebugLogSpace > 0)
                            {
                                geo.Transform(component2logTransform);
                                plane.Transform(component2logTransform);
                            }
                            else
                            {
                                geo.Transform(component2boardTransform);
                                plane.Transform(component2boardTransform);
                            }

                            Component2LogTransforms[Components[ele].Name] = component2logTransform;
                            PlacementTransforms[Components[ele].Name] = transforms[ele];
                            ComponentBoardMap[Components[ele].Name] = board.Name;

                            foreach (var face in geo.Faces)
                            {
                                var normal = face.NormalAt(face.Domain(0).Mid, face.Domain(1).Mid);
                                var dot = normal * plane.ZAxis;
                                if ((1 - Math.Abs(dot)) < 0.00001)
                                {
                                    var top = dot > 0;
                                    //top ^= face.OrientationIsReversed;

                                    if (top)
                                    {
                                        var outer = face.OuterLoop;
                                        rhinoBoard.Objects.AddCurve(outer.To3dCurve(), new ObjectAttributes { Name = Components[ele].Name, LayerIndex = layerTopOutlineInnerIndex });
                                        foreach (var loop in face.Loops)
                                        {
                                            if (loop.LoopIndex == outer.LoopIndex) continue;
                                            rhinoBoard.Objects.AddCurve(loop.To3dCurve(), new ObjectAttributes { Name = Components[ele].Name, LayerIndex = layerTopOutlineIndex });
                                        }
                                        /*
                                        rhinoBoard.Objects.AddLine(new Line(plane.Origin, normal, 100.0), new ObjectAttributes
                                        {
                                            Name = Components[ele].Name,
                                            LayerIndex = layerTopOutlineIndex,
                                            ObjectDecoration = ObjectDecoration.EndArrowhead
                                        });
                                        */
                                    }
                                    else
                                    {
                                        var outer = face.OuterLoop;
                                        rhinoBoard.Objects.AddCurve(outer.To3dCurve(), new ObjectAttributes { Name = Components[ele].Name, LayerIndex = layerTopOutlineInnerIndex });
                                        foreach (var loop in face.Loops)
                                        {
                                            if (loop.LoopIndex == outer.LoopIndex) continue;
                                            rhinoBoard.Objects.AddCurve(loop.To3dCurve(), new ObjectAttributes { Name = Components[ele].Name, LayerIndex = layerBottomOutlineIndex });
                                        }
                                        /*
                                        rhinoBoard.Objects.AddLine(new Line(plane.Origin, normal, 100.0), new ObjectAttributes
                                        {
                                            Name = Components[ele].Name,
                                            LayerIndex = layerBottomOutlineIndex,
                                            ObjectDecoration = ObjectDecoration.EndArrowhead
                                        });
                                        */
                                    }
                                }
                            }

                            foreach (var ol in Components[ele].Outline)
                            {
                                var outline = ol.Duplicate();
                                outline.Transform(transforms[ele]);

                                if (Settings.DebugLogSpace > 0)
                                {
                                    outline.Transform(board2logWorld);
                                    outline.Transform(logWorld2logLocal);
                                }
                                //ids.Add(rhinoBoard.Objects.AddPolyline(outline, new ObjectAttributes { Name = Components[ele].Name, LayerIndex = layerTopOutlineIndex }));
                            }

                            foreach (Drilling drilling in Components[ele].Objects)
                            {
                                var axis = drilling.Axis;

                                if (Settings.DebugLogSpace > 0)
                                    axis.Transform(component2logTransform);
                                else
                                    axis.Transform(component2boardTransform);

                                Rhino.Geometry.Intersect.Intersection.LinePlane(axis, topPlane, out double t0);
                                Rhino.Geometry.Intersect.Intersection.LinePlane(axis, bottomPlane, out double t1);

                                axis = new Line(axis.PointAt(t0), axis.PointAt(t1));

                                if (RhinoMath.EpsilonEquals(drilling.Diameter, 6.0, 1e-10))
                                    rhinoBoard.Objects.AddLine(axis, new ObjectAttributes { Name = Components[ele].Name, LayerIndex = layerDrill6mmIndex });
                                else if (RhinoMath.EpsilonEquals(drilling.Diameter, 12.0, 1e-10))
                                    rhinoBoard.Objects.AddLine(axis, new ObjectAttributes { Name = Components[ele].Name, LayerIndex = layerDrill12mmIndex });

                            }

                            ids.Add(rhinoBoard.Objects.AddBrep(geo, new ObjectAttributes { Name = Components[ele].Name, LayerIndex = layerBrepIndex, WireDensity = -1 }));
                        }

                        rhinoBoard.Write3dmFile(System.IO.Path.Combine(Settings.ProjectDirectory, "fabrication", board.Name + ".3dm"), new Rhino.FileIO.FileWriteOptions { FileVersion = 5 });
                        //rhinoDoc.Groups.Add(boards[kvp.Key].Name, ids);
                    }
                }
                #endregion


                #region Set material orientations in FE model

                if (ValidMesh)
                {

                    model.Mesh.ElementOrientations.Clear();

                    foreach (var component in Components)
                    {
                        if (!Component2LogTransforms.ContainsKey(component.Name))
                        {
                            Logger.Error($"WARNING: component '{component.Name}' does not have a transform.");
                            continue;
                        }
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

                    var distribution = model.Mesh.Distributions["dist"];
                    distribution.Labels = model.Mesh.ElementOrientations.Select(x => x.Key).ToArray();

                    model.Name = string.Format("EMA{0}", DateTime.UtcNow.ToString("yyMMddHHmmss"));
                }

                // Write input file
                var outputDirectory = Settings.CalculixOutputPath;
                outputDirectory = System.IO.Path.GetFullPath(outputDirectory);
                var inp_path = System.IO.Path.Combine(outputDirectory, model.Name + ".inp");

                if (ValidMesh)
                {
                    Logger.Info("Writing INP file: {0}", inp_path);
                    FileInOut.Output.CalculixFileWriter.Write(inp_path, model);
                }
                else
                {
                    Logger.Error("");
                    Logger.Error("No model constructed. No INP file to write.");
                    Logger.Error("");
                }
            
                Logger.Info("Saving debug Rhino file...");
                var rhino_path = System.IO.Path.Combine(Settings.CalculixOutputPath, model.Name + ".3dm");

                var docViews = rhinoDoc.Views.GetStandardRhinoViews();
                foreach (var docView in docViews)
                {
                    docView.MainViewport.ZoomExtents();
                    docView.MainViewport.DisplayMode = Rhino.Display.DisplayModeDescription.GetDisplayMode(Rhino.Display.DisplayModeDescription.ShadedId);
                }
                rhinoDoc.Write3dmFile(rhino_path, new Rhino.FileIO.FileWriteOptions());
                Logger.Info("Saved to {0}", rhino_path);

                rhinoDoc.Objects.Clear();
                rhinoDoc.Groups.Clear();
                rhinoDoc.Layers.Clear();

                #endregion


                #region Launch CalculiX
                if (ValidMesh)
                {
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
                }

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
                //var results = new Results(this);
                //results.Run(frd_path, inp_path, rhino_path, ref currentMin, ref currentMax, model);

                // Load results and get new min/max values
                FeResults results = null;

                try
                {
                    if (System.IO.File.Exists(frd_path))
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
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }

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

                var resultsTransport = new Speckle.Core.Transports.SQLiteTransport(Settings.ProjectDirectory, Settings.ModelDirectory, "results");

                // Send results object to database
                var resString = Task.Run(async () => await Speckle.Core.Api.Operations.Send(allocationResults, new List<Speckle.Core.Transports.ITransport> { resultsTransport }, false)).Result;

                // Write to simulation commit file
                var resultsLogPath = System.IO.Path.Combine(Settings.ProjectDirectory, Settings.ModelDirectory, "results.log");
                System.IO.File.AppendAllLines(resultsLogPath, new string[] { resString });
                Logger.Info("Results ID: {0}", resString);

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
