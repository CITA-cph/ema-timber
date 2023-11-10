using OpenNestLib;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawLamAllocator
{
    internal class Nesting
    {
        private Allocator m_alloc;
        internal static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public Nesting(Allocator alloc)
        {
            m_alloc = alloc;
        }

        public double Run(int seed, IList<Polyline> elements, IList<Polyline> sheets, out Transform[] transforms, out int[] sheetIds, int iterations)
        {
            transforms = new Transform[elements.Count];
            sheetIds = new int[elements.Count];

            var nestContext = new NestingContext();
            ONest.Config.spacing = 20;
            ONest.Config.sheetSpacing = 5;
            ONest.Config.rotations = 2;
            ONest.Config.placementType = PlacementTypeEnum.squeeze;
            ONest.Config.seed = seed;
            //ONest.Config.simplify = true;

            if (elements.Count < 1 || sheets.Count < 1)
                return double.NaN;

            for (int i = 0; i < elements.Count; ++i)
            {
                nestContext.AddPolygon(elements[i], i);
            }

            for (int i = 0; i < sheets.Count; ++i)
            {
                nestContext.AddSheet(sheets[i], i);
            }

            Logger.Info("Iterations:            {0}", nestContext.Iterations);
            Logger.Info("Material utilization:  {0}", nestContext.MaterialUtilization);
            Logger.Info("Unused sheets:         {0}", nestContext.SheetsNotUsed);
            Logger.Info("Seed:                  {0}", ONest.Config.seed);

            nestContext.StartNest();

            var best = nestContext.Current;

            for (int i = 0; i < iterations; ++i)
            {
                Logger.Info("Nesting iteration {0}", i);

                nestContext.NestIterate();
                if (best == null) best = nestContext.Current;
                else
                if (nestContext.Current.fitness > best.fitness)
                {
                    best = nestContext.Current;
                    Logger.Info("    better fitness: {0}", best.fitness);
                }
            }

            Logger.Info("Iterations:            {0} Best fitness: {1}", nestContext.Iterations, best.fitness);
            Logger.Info("Material utilization:  {0}", nestContext.MaterialUtilization);
            Logger.Info("Unused sheets:         {0}", nestContext.SheetsNotUsed);

            for (int i = 0; i < nestContext.Sheets.Count; ++i)
            {
                var sheet = nestContext.Sheets[i];
                Logger.Info("Sheet {0}", sheet.id);

                if (sheet.children != null)
                    for (int j = 0; j < sheet.children.Count; ++j)
                    {
                        var child = sheet.children[j];
                        Logger.Info("    {0}", child.Id);
                    }
            }

            Logger.Info("Current: {0}", nestContext.Current);
            Logger.Info("");

            for (int i = 0; i < best.placements.Length; ++i)
            {
                var placements = best.placements[i];
                Logger.Info("SheetPlacement {0}, numPlacements {1}", i, placements.Count);

                for (int j = 0; j < placements.Count; ++j)
                {
                    var placement = placements[j];
                    Logger.Info("    Sheet ID {0}, numPlacements {1} numSheetPlacements {2}",
                        placement.sheetId, placement.placements.Count, placement.sheetplacements.Count);

                    for (int k = 0; k < placement.sheetplacements.Count; ++k)
                    {
                        var sheetPlacement = placement.sheetplacements[k];
                        Logger.Info("        place id {0} x {1} y {2} rotation {3}",
                            sheetPlacement.id, sheetPlacement.x, sheetPlacement.y, sheetPlacement.rotation);

                        transforms[sheetPlacement.id] =
                            Transform.Translation(sheetPlacement.x, sheetPlacement.y, 0) *
                        Transform.Rotation(Rhino.RhinoMath.ToRadians(sheetPlacement.rotation), Point3d.Origin);

                        sheetIds[sheetPlacement.id] = placement.sheetId;
                    }
                }
            }

            return (double)best.fitness;

        }

    }
}
