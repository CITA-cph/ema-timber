using Rhino.Geometry;

namespace EmaTimber
{
    /// <summary>
    /// A planar, multi-layered beam with bracing at both ends.
    /// Width is defined by lamella thickness and count. Height
    /// is arbitrary.
    /// </summary>
    public class BracedBeam
    {
        public Plane Plane;
        public double LamellaThickness = 18;
        public int NumLamellas = 3;

        // Cross-section dimensions
        public double Width
        {
            get
            {
                return LamellaThickness * NumLamellas;
            }
        }

        public double Height
        {
            get; set;
        }

        // Brace dimensions
        public double BraceHeight = 240;
        public double BraceLength1 = 100;
        public double BraceLength2 = 100;
        public double BraceTransitionLength1 = 100;
        public double BraceTransitionLength2 = 100;

        // Beam centreline
        public Line Centreline;

        // Beam profile
        public Polyline Outline;

        public BracedBeam(Line line, Vector3d up, double width = 54, double height = 108, int numLamellas = 3)
        {
            Centreline = line;
            var xaxis = Vector3d.CrossProduct(up, line.Direction);
            Plane = new Plane(line.From, xaxis, Vector3d.CrossProduct(line.Direction, xaxis));
            Height = height;
            NumLamellas = numLamellas;
            LamellaThickness = width / numLamellas;
        }

        public void CreateOutline()
        {
            // Half-dimensions
            double hwidth = Width / 2;
            double hheight = Height / 2;
            double notchHeight = hheight;

            var endBraceDistance = 150;
            var braceTransitionDistance = 150;

            // Height or thickness of braced ends
            var braceHeight = BraceHeight - hheight;

            // Total length of element
            var length = Centreline.Length;

            // Construct outline in element-space
            Outline = new Polyline();

            // Check if the length is too short to have space between bracings
            var neckLength = length - endBraceDistance * 2;

            bool fullNeck = length > (BraceLength1 * 2 + BraceTransitionLength1 * 2);
            bool halfNeck = length > (BraceLength1 * 2);

            fullNeck ^= BraceHeight == Height;
            halfNeck ^= BraceHeight == Height;

            // Make partial brace/neck
            if (halfNeck)
            {
                double notchHalfWidth = length / 2 - BraceLength1;
                double t = notchHalfWidth / braceTransitionDistance;
                notchHeight = braceHeight + t * (hheight - braceHeight);
            }

            Outline.Add(new Point3d(0, hheight, 0));
            Outline.Add(new Point3d(0, hheight, length));
            Outline.Add(new Point3d(0, -braceHeight, length));

            if (fullNeck)
            {
                Outline.Add(new Point3d(0, -braceHeight, length - BraceLength1));
                Outline.Add(new Point3d(0, -hheight, length - BraceLength1 - BraceTransitionLength1));
                Outline.Add(new Point3d(0, -hheight, BraceLength1 + BraceTransitionLength1));
                Outline.Add(new Point3d(0, -braceHeight, BraceLength1));
            }
            else if (halfNeck)
            {
                Outline.Add(new Point3d(0, -braceHeight, length - BraceLength1));
                Outline.Add(new Point3d(0, -notchHeight, length / 2));
                Outline.Add(new Point3d(0, -braceHeight, BraceLength1));
            }

            Outline.Add(new Point3d(0, -braceHeight, 0));
            Outline.Add(new Point3d(0, hheight, 0));
        }

        public Brep ToBrep()
        {
            if (Outline == null)
                CreateOutline();

            // Create extrusion of beam profile
            var profile = Outline.ToNurbsCurve();
            profile.Transform(Transform.Translation(Width / 2, 0, 0));

            var extrusion = Extrusion.Create(profile, -Width, true);
            extrusion.Transform(Transform.PlaneToPlane(Plane.WorldXY, Plane));

            return extrusion.ToBrep(true);
        }

        public Brep[] LamellaBreps()
        {
            if (Outline == null)
                CreateOutline();
            var breps = new Brep[NumLamellas];

            for (int i = 0; i < NumLamellas; ++i)
            {
                // Create extrusion of beam profile
                var profile = Outline.ToNurbsCurve();
                profile.Transform(Transform.Translation(Width / 2 - LamellaThickness * i, 0, 0));

                var extrusion = Extrusion.Create(profile, -LamellaThickness, true);
                extrusion.Transform(Transform.PlaneToPlane(Plane.WorldXY, Plane));

                breps[i] = extrusion.ToBrep(true);
            }

            return breps;
        }
    }
}
