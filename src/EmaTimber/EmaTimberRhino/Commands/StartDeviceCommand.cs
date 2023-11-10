using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using SharpPcap;

namespace EmaTimber.Commands
{
    [System.Runtime.InteropServices.Guid("ae4cb26b-925f-46a2-9253-dca90e310bca")]
    public class StartDeviceCommand : Command
    {
        public StartDeviceCommand()
        {
            Instance = this;
        }

        public static StartDeviceCommand Instance
        {
            get; private set;
        }

        public override string EnglishName
        {
            get { return "EmaStartDevice"; }
        }

        internal static void Device_OnPacketArrival(object s, PacketCapture e)
        {
            Int32 x, y, z;
            var packet = e.GetPacket();
            var bytes = packet.Data;
            var offset = 14;

            if (bytes.Length < offset + 12) return;

            var cmd = (int)bytes[offset];

            if (cmd != 34) return;

            x = BitConverter.ToInt32(bytes, 16 + offset);
            y = BitConverter.ToInt32(bytes, 20 + offset);
            z = BitConverter.ToInt32(bytes, 24 + offset);
            double dx = (double)x / ETContext.FactorXY, dy = (double)y / ETContext.FactorXY, dz = (double)z / ETContext.FactorZ;

            ETContext.CncPosition = new Point3d(dx, -dy, dz);

            //if (EmaTimberPlugin.LastCncPosition.DistanceTo(EmaTimberPlugin.CncPosition) < 0.01)
            //{
            ETContext.LastCncPosition = ETContext.CncPosition;
                Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

            //}

        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            if (ETContext.CncAddress == null) return Result.Failure;

            //ETContext.CncConnection = ETContext.GetPcapDevice(ETContext.CncFriendlyName);
            ETContext.CncConnection = ETContext.GetPcapDevice(ETContext.CncAddress);
            //if (ETContext.CncConnection == null)
            //{
            //    ETContext.FindDevice(PhysicalAddress.Parse("8C8CAA0F614E"));
            //}

            //RhinoApp.WriteLine("Configuring device...");
            var config = new DeviceConfiguration { 
                Immediate = true,
                Mode = DeviceModes.DataTransferUdp,
                //Mode = DeviceModes.MaxResponsiveness, 
                //BufferSize = 1, 
                //KernelBufferSize = 1, 
                //MinToCopy = 0, 
                //ReadTimeout = 3
            };

            RhinoApp.WriteLine("Opening CNC connection...");
            ETContext.CncConnection.Open(config);
            if (!ETContext.CncConnection.Opened)
            {
                RhinoApp.WriteLine("Failed.");
                return Result.Failure;
            }

            ETContext.CncConnection.NonBlockingMode = true;

            ETContext.CncConnection.OnPacketArrival += Device_OnPacketArrival;
            ETContext.CncConnection.StartCapture();
            //Rhino.Display.DisplayPipeline.PostDrawObjects += DisplayPipeline_DrawCncPosition;

            // Setup laser
            RhinoApp.WriteLine("Opening laser connection...");
            if (ETContext.DistanceSensor == null)
                ETContext.DistanceSensor = new OD2Interface(ETContext.LaserPort); // Change to allow picking of port

            ETContext.DistanceSensor.ContinuousMeasure = true;
            ETContext.DistanceSensor.SendCommand("LASER_ON");
            ETContext.DistanceSensor.SendCommand("START_MEASURE");

            return Result.Success;
        }
    }
}
