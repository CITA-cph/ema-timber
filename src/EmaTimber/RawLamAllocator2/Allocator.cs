
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

                var distribution = model.Mesh.Distributions["dist"];
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
                var results = new Results(this);
                results.Run(frd_path, inp_path, rhino_path, ref currentMin, ref currentMax, model);

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
