using DeepSight;
using RawLamb;
using Rhino;
using Speckle.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawLamAllocator
{
    internal partial class Allocator
    {
        public void LoadModel(RhinoDoc rhinoDoc)
        {
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

                        board_meshes.Add(converter.MeshToNative(speckleBoard["mesh"] as Objects.Geometry.Mesh));

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
        }
    }
}
