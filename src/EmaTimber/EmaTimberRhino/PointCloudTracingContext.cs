using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace EmaTimber
{
    internal class PointCloudTracingContext
    {
        public PointCloudTracingContext() { }

        public bool Running = false;
        public PointCloud Cloud = null;
        public Point3d LastPoint = Point3d.Unset;

    }
}
