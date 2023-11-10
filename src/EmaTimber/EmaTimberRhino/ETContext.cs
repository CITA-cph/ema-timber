using EmaTimber;
using Rhino.Geometry;
using SharpPcap.LibPcap;
using SharpPcap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using PacketDotNet;
using System.Net;
using Rhino;
using EmaTimberRhino;
using Rhino.Display;

namespace EmaTimber
{
    internal static class ETContext
    {
        public static LibPcapLiveDevice CncConnection { get; set; }
        public static PacketArrivalEventHandler CncConnectionPacketArrivalHandler;
        public static double FactorXY = 800;
        public static double FactorZ = 1333.3333;
        public static Point3d CncPosition;
        public static Point3d LastCncPosition;
        public static Point3d LaserPoint;

        public static string LaserPort = "COM5";
        public static string CncFriendlyName = "";
        public static PhysicalAddress CncAddress = null;

        internal static PointCloudTracingContext PointCloudContext = new PointCloudTracingContext();
        internal static SimulationContext SimulationContext = new SimulationContext();

        public static OD2Interface DistanceSensor;

        public static DisplayMaterial MeshMaterial = new DisplayMaterial();

        public static void FindDevice(PhysicalAddress address)
        {
            CncConnection = GetPcapDevice(address);
        }

        public static List<Tuple<string,PhysicalAddress>> GetAvailablePcapDeviceNames()
        {
            var pcapInterfaces = PcapInterface.GetAllPcapInterfaces();

            var interfaces = new List<Tuple<string, PhysicalAddress>>();

            foreach (var inf in pcapInterfaces)
            {
                if (inf.MacAddress == null || string.IsNullOrEmpty(inf.FriendlyName)) continue;

                interfaces.Add(new Tuple<string, PhysicalAddress>(inf.FriendlyName, inf.MacAddress));

            }

            return interfaces;
        }

        public static LibPcapLiveDevice GetPcapDevice(string friendlyName)
        {

            var interfaces = PcapInterface.GetAllPcapInterfaces();
            var inf = interfaces.FirstOrDefault(ni => ni.Name == friendlyName);
            if (inf == null) throw new Exception("Failed to find interface " + friendlyName + ".");

            var device = new LibPcapLiveDevice(inf);
            LinkLayers link;
            try
            {
                device.Open();
                link = device.LinkType;
            }
            catch (PcapException ex)
            {
                RhinoApp.WriteLine(ex.Message);
            }

            return device;
        }

        public static LibPcapLiveDevice GetPcapDevice(PhysicalAddress address = null)
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var inf in PcapInterface.GetAllPcapInterfaces())
            {
                var friendlyName = inf.FriendlyName ?? string.Empty;
                if (friendlyName.ToLower().Contains("loopback") || friendlyName == "any")
                {
                    continue;
                }
                if (friendlyName == "virbr0-nic")
                {
                    continue;
                }
                var nic = nics.FirstOrDefault(ni => ni.Name == friendlyName);
                if (nic?.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }
                if (address != null && inf.MacAddress == address) return new LibPcapLiveDevice(inf);

                var device = new LibPcapLiveDevice(inf);
                LinkLayers link;
                try
                {
                    device.Open();
                    link = device.LinkType;
                }
                catch (PcapException ex)
                {
                    Console.WriteLine(ex);
                    continue;
                }

                if (address != null)
                {
                    if (device.Interface.MacAddress.Equals(address))
                    {
                        return device;
                    }
                }
                else if (link == LinkLayers.Ethernet)
                {
                    return device;
                }
            }
            throw new InvalidOperationException("No ethernet pcap supported devices found, are you running" +
                                           " as a user with access to adapters (root on Linux)?");
        }

        internal static void DisplayPipeline_CalculateBoundingBox(object sender, Rhino.Display.CalculateBoundingBoxEventArgs e)
        {
            BoundingBox bb = new BoundingBox();
            bb.Union(CncPosition);
            bb.Union(LaserPoint);
            if (PointCloudContext.Cloud != null)
                bb.Union(PointCloudContext.Cloud.GetBoundingBox(false));
            if (SimulationContext.SimulationMesh != null)
                bb.Union(SimulationContext.SimulationMesh.GetBoundingBox(true));

            e.IncludeBoundingBox(bb);
        }

        internal static void DisplayPipeline_PostDrawObjects(object sender, Rhino.Display.DrawEventArgs e)
        {
            // Draw CNC widget
            Plane tcp = new Plane(ETContext.CncPosition, Vector3d.ZAxis);
            Transform xform = Transform.PlaneToPlane(Plane.WorldXY, tcp);

            Circle c = new Circle(10.0);
            double axis_length = 15;
            Line xaxis = new Line(new Point3d(-axis_length, 0, 0), new Point3d(axis_length, 0, 0));
            Line yaxis = new Line(new Point3d(0, -axis_length, 0), new Point3d(0, axis_length, 0));

            c.Transform(xform);
            xaxis.Transform(xform);
            yaxis.Transform(xform);
            e.Display.DrawCircle(c, System.Drawing.Color.Red);
            e.Display.DrawLine(xaxis, System.Drawing.Color.Red);
            e.Display.DrawLine(yaxis, System.Drawing.Color.Red);

            // Draw laser point

            if (ETContext.DistanceSensor != null)
            {
                ETContext.LaserPoint = new Point3d(tcp.OriginX, tcp.OriginY, tcp.OriginZ - ETContext.DistanceSensor.MeasureValue);
                e.Display.DrawPoint(ETContext.LaserPoint, Rhino.Display.PointStyle.X, 3, System.Drawing.Color.Red);

                var pCtx = ETContext.PointCloudContext;
                if (pCtx.Running)
                    if (pCtx.LastPoint.DistanceToSquared(ETContext.LaserPoint) >= 1.0)
                    {
                        pCtx.Cloud.Add(ETContext.LaserPoint);
                        pCtx.LastPoint = ETContext.LaserPoint;
                    }
            }

            // Draw point cloud

            if (PointCloudContext != null && PointCloudContext.Cloud != null)
                e.Display.DrawPointCloud(PointCloudContext.Cloud, 1);

            if (SimulationContext != null)
            {
                foreach (var part in SimulationContext.Parts)
                {
                    if (part.Value.Mesh != null)
                    {
                        e.Display.DrawMeshShaded(part.Value.Mesh, part.Value.Material);
                        e.Display.DrawMeshWires(part.Value.Mesh, System.Drawing.Color.DarkGray, 1);

                    }
                }
            }

        }
    }
}
