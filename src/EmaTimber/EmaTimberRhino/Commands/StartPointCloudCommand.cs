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
    [System.Runtime.InteropServices.Guid("5f6efa3d-4c46-45a7-a27f-724c91dec422")]
    public class TracePointCloudCommand : Command
    {
        private PointCloudTracingContext _context;

        public TracePointCloudCommand()
        {
            Instance = this;
            _context = ETContext.PointCloudContext;
        }

        public static TracePointCloudCommand Instance
        {
            get; private set;
        }

        public override string EnglishName
        {
            get { return "EmaStartPointCloud"; }
        }


        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            _context.Cloud = new PointCloud();
            _context.LastPoint = ETContext.CncPosition;
            _context.Running = true;

            return Result.Success;
        }
    }
}
