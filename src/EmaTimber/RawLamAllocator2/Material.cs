using CaeModel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawLamAllocator
{
    internal static partial class Allocator
    {
        static List<string> OrthotropicWoodMaterial()
        {
            List<string> file = new List<string>();

            file.Add("*Material, Name=Wood");
            file.Add("*ELASTIC, TYPE=ENGINEERING CONSTANTS");

            file.Add("9700e6,400e6,220e6,0.35,0.6,0.55,400e6,250e6,");
            file.Add("25e6, 20");
            //file.Add("600e6,600e6,12000e6,0.558,0.038,0.015,40e6,700e6,");
            //file.Add("700e6, 20");
            file.Add("*DENSITY");
            file.Add("450");
            file.Add("*EXPANSION, TYPE=ORTHO, ZERO=13");
            file.Add("0.03, 0.36, 0.78");
            file.Add("*Conductivity");
            file.Add("200");
            file.Add("*Specific heat");
            file.Add("900");

            return file;
        }


        public static void AddMaterialOrientations(string inp_path, FeModel model, int[] indices, Vector3d[] xdir, Vector3d[] ydir)
        {
            var file = System.IO.File.ReadAllLines(inp_path);
            var new_file = new List<string>();

            var material_index = 0;
            var section_index = 0;
            string[] subfile;

            for (int i = 0; i < file.Length; ++i)
            {
                if (file[i].StartsWith("*Material"))
                {
                    subfile = new string[i];
                    Array.Copy(file, subfile, i);
                    material_index = i;
                    new_file.AddRange(subfile);
                    //throw new Exception("Added initial part");
                }
                else if (file[i].StartsWith("*Solid section"))
                {
                    section_index = i;
                    break;
                }
            }

            new_file.AddRange(OrthotropicWoodMaterial());

            // Add distribution
            new_file.Add("*DISTRIBUTION,NAME=dist");
            new_file.Add(",0.,0.,1.,0.,1.,0.");

            for (int i = 0; i < indices.Length; ++i)
            {
                var dx = xdir[i];
                var dy = ydir[i];

                new_file.Add(string.Format("{0},{1:0.0###},{2:0.0###},{3:0.0###},{4:0.0###},{5:0.0###},{6:0.0###}",
                  indices[i],
                  dx.X, dx.Y, dx.Z,
                  dy.X, dy.Y, dy.Z));
            }

            // Create orientation
            new_file.Add("*ORIENTATION,NAME=OR");
            new_file.Add("dist");

            subfile = new string[file.Length - section_index];
            Array.Copy(file, section_index, subfile, 0, subfile.Length);
            subfile[0] = subfile[0] + ", ORIENTATION=OR";
            new_file.AddRange(subfile);

            System.IO.File.WriteAllLines(inp_path, new_file);

            //Log.AddRange(new_file);
        }


        public static void SetGlobalOutput(string inp_path, bool yes)
        {
            var file = System.IO.File.ReadAllLines(inp_path);
            var new_file = new List<string>();


            string yesno = yes ? "YES" : "NO";
            string new_line = "";

            for (int i = 0; i < file.Length; ++i)
            {
                if (file[i].StartsWith("*El file"))
                {
                    new_file.Add(file[i] + ", GLOBAL=" + yesno);
                }
                else
                    new_file.Add(file[i]);
            }

            System.IO.File.WriteAllLines(inp_path, new_file);

            //Log.AddRange(new_file);
        }


    }
}
