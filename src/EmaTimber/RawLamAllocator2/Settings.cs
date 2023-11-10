using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace RawLamAllocator
{
    [XmlRoot("rawlam_settings")]
    public class Settings
    {
        [XmlElement("project_directory")]
        public string ProjectDirectory { get; set; }

        [XmlElement("model_directory")]
        public string ModelDirectory { get; set; }

        [XmlElement("elements_source")]
        public string ElementsSource { get; set; }

        [XmlElement("elements_database_name")]
        public string ElementsDatabaseName { get; set; }

        [XmlElement("elements_stream_id")]
        public string ElementsStreamId { get; set; }

        [XmlElement("boards_source")]
        public string BoardsSource { get; set; }

        [XmlElement("boards_database_name")]
        public string BoardsDatabaseName { get; set; }

        [XmlElement("boards_stream_id")]
        public string BoardsStreamId { get; set; }

        [XmlElement("ccx_exe_path")]
        public string CalculixExePath { get; set; }

        [XmlElement("ccx_output_path")]
        public string CalculixOutputPath { get; set; }

        [XmlElement("board_offset_distance")]
        public double BoardOffset { get; set; }

        [XmlElement("fe_mesh_size_min")]
        public double FeMeshSizeMin { get; set; }

        [XmlElement("fe_mesh_size_max")]
        public double FeMeshSizeMax { get; set; }

        [XmlElement("global_seed")]
        public int GlobalSeed { get; set; }

        [XmlElement("max_iterations")]
        public int MaxIterations { get; set; }

        [XmlElement("debug_max_elements")]
        public int DebugMaxElements { get; set; }

        [XmlElement("debug_log_space")]
        public int DebugLogSpace { get; set; }

        public Settings() 
        {
            ProjectDirectory = "";
            ModelDirectory = "";
            ElementsDatabaseName = "";
            BoardsDatabaseName = "";
            CalculixExePath = "";
            CalculixOutputPath = "";

            BoardOffset = 5.0;
            FeMeshSizeMin = 1.0;
            FeMeshSizeMax = 20.0;
            DebugMaxElements = int.MaxValue;
            DebugLogSpace = 0;
            GlobalSeed = 7777;
        }

        public static Settings Read(string filepath)
        {
            var serializer = new XmlSerializer(typeof(Settings));
            using (var reader = new FileStream(filepath, FileMode.Open))
            {
                var settings = (Settings)serializer.Deserialize(reader);
                settings.ProjectDirectory = System.Environment.ExpandEnvironmentVariables(settings.ProjectDirectory);
                settings.ModelDirectory = System.Environment.ExpandEnvironmentVariables(settings.ModelDirectory);
                settings.CalculixExePath = System.Environment.ExpandEnvironmentVariables(settings.CalculixExePath);
                settings.CalculixOutputPath = System.Environment.ExpandEnvironmentVariables(settings.CalculixOutputPath);

                return settings;
            }
        }

        public void Write(string filepath) 
        {
            var serializer = new XmlSerializer(typeof(Settings));
            using (var writer = new StreamWriter(filepath))
            {
                serializer.Serialize(writer, this);
            }
        }

        public void Dump(Logger logger)
        {
            foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(this))
            {
                string name = descriptor.Name;
                object value = descriptor.GetValue(this);
                logger.Info("{0} = {1}", name, value);
            }
        }
    }
}
