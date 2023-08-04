using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Runtime.InProcess;

using OpenNest;
using OpenNestLib;
using NLog;
using System.Diagnostics;
using System.Threading;
using System.Configuration;

using Speckle.Core.Models;
using CaeGlobals;
using RawLam;
using CaeModel;
using CaeMesh;
using FileInOut;


namespace RawLamAllocator
{
    internal partial class Program
    {
        static Program()
        {
            RhinoInside.Resolver.Initialize();
        }

        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        static void SetupLogging()
        {
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
        }

        [System.STAThread]
        static void Main(string[] args)
        {
            using (new RhinoCore(args))
            {
                var alloc = new Allocator();

                if (args.Length > 0)
                    alloc.Run(args[0]);
                else
                    alloc.Run();
            }
        }
    }
}
