using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using SharpPcap;
using static Rhino.UI.Controls.CollapsibleSectionImpl;

namespace EmaTimber.Commands
{
    [System.Runtime.InteropServices.Guid("dec1fa23-a58f-433c-9770-39615361210b")]
    public class StopDeviceCommand : Command
    {
        public StopDeviceCommand()
        {
            Instance = this;
        }

        public static StopDeviceCommand Instance
        {
            get; private set;
        }

        public override string EnglishName
        {
            get { return "EmaStopDevice"; }
        }


        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {

            RhinoApp.WriteLine("Closing CNC connection...");

            if (ETContext.CncConnection != null)
            {
                ETContext.CncConnection.StopCapture();
                ETContext.CncConnection.OnPacketArrival -= StartDeviceCommand.Device_OnPacketArrival;
                ETContext.CncConnection.Close();
            }

            if (ETContext.DistanceSensor != null)
            {
                ETContext.DistanceSensor.ContinuousMeasure = true;
                ETContext.DistanceSensor.SendCommand("STOP_MEASURE");
                ETContext.DistanceSensor.SendCommand("LASER_OFF");
            }

            return Result.Success;
        }
    }
}
