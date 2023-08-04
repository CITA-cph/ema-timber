using System;
using System.Collections.Generic;
using System.Linq;

using System.Threading.Tasks;
using CaeModel;
using CaeResults;
using Speckle.Core.Models;

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

    internal class Results
    {
        private Allocator m_alloc;
        internal static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        
        public Results(Allocator alloc)
        {
            m_alloc = alloc;
        }

        public void Run(string frd_path, string inp_path, string rhino_path, ref double currentMin, ref double currentMax, FeModel model)
        {
            FeResults results = null;
            try
            {
                results = FrdFileReader.Read(frd_path);
                var resultsTransport = new Speckle.Core.Transports.SQLiteTransport(m_alloc.Settings.ProjectDirectory, m_alloc.Settings.ModelDirectory, "results");

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

                    for (int i = 0; i < m_alloc.Components.Count; ++i)
                    {
                        allocationResults.ComponentNames.Add(m_alloc.Components[i].Name);
                        allocationResults.ComponentTransforms.Add(new Objects.Other.Transform(m_alloc.Component2LogTransforms[m_alloc.Components[i].Name].ToFloatArray(true)));
                        allocationResults.ComponentBoards.Add(m_alloc.ComponentBoardMap[m_alloc.Components[i].Name]);
                        allocationResults.ComponentLogs.Add(m_alloc.BoardLogMap[m_alloc.ComponentBoardMap[m_alloc.Components[i].Name]]);
                    }

                    // Send results object to database
                    var resString = Task.Run(async () => await Speckle.Core.Api.Operations.Send(allocationResults, new List<Speckle.Core.Transports.ITransport> { resultsTransport }, false)).Result;
                    // Write to simulation commit file
                    var resultsLogPath = System.IO.Path.Combine(m_alloc.Settings.ProjectDirectory, m_alloc.Settings.ModelDirectory, "results.log");
                    System.IO.File.AppendAllLines(resultsLogPath, new string[] { resString });
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }

        }
    }
}
