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
    [System.Runtime.InteropServices.Guid("012baac7-b64e-4992-8ffc-b2e87c219bf1")]
    public class StopPointCloudCommand : Command
    {
        private PointCloudTracingContext _context;

        public StopPointCloudCommand()
        {
            Instance = this;
            _context = ETContext.PointCloudContext;
        }

        public static StopPointCloudCommand Instance
        {
            get; private set;
        }

        public override string EnglishName
        {
            get { return "EmaStopPointCloud"; }
        }


        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            doc.Objects.AddPointCloud(_context.Cloud);
            //_context.Cloud = new PointCloud();
            _context.Running = false;

            return Result.Success;
        }
    }
}
