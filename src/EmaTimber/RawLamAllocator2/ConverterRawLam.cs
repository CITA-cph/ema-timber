using RawLamb;
using Rhino.Geometry;
using Speckle.Core.Kits;
using Speckle.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawLamAllocator
{
    internal class ConverterRawLam : ISpeckleConverter
    {
        static readonly string[] ConvertableToNative = new string[] { "Log", "Board" };
        static readonly Type[] ConvertableToSpeckle = new Type[] { typeof(RawLamb.Log), typeof(RawLamb.Board) };

        public string Description => "Converter for RawLam objects.";

        public string Name => "RawLam Converter";

        public string Author => "Tom Svilans";

        public string WebsiteOrEmail => "https://tomsvilans.com";

        public ProgressReport Report => throw new NotImplementedException();

        public ReceiveMode ReceiveMode { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool CanConvertToNative(Base @object)
        {
            return ConvertableToNative.Contains(@object.speckle_type);
        }

        public bool CanConvertToSpeckle(object @object)
        {
            return ConvertableToSpeckle.Contains(@object.GetType());
        }

        public object ConvertToNative(Base @object)
        {
            switch (@object.speckle_type)
            {
                case ("Log"):
                    return ConvertToNativeLog(@object);
                    break;
                default:
                    return null;
                    break;
            }
        }

        public List<object> ConvertToNative(List<Base> objects)
        {
            var output = new List<object>();
            foreach (var obj in objects)
            {
                output.Add(ConvertToNative(obj));
            }
            return output;
        }

        public Base ConvertToSpeckle(object @object)
        {
            throw new NotImplementedException();
        }

        public List<Base> ConvertToSpeckle(List<object> objects)
        {
            var output = new List<Base>();
            foreach (var obj in objects)
            {
                output.Add(ConvertToSpeckle(obj));
            }
            return output;
        }

        public IEnumerable<string> GetServicedApplications()
        {
            throw new NotImplementedException();
        }

        public void SetContextDocument(object doc)
        {
            throw new NotImplementedException();
        }

        public void SetContextObjects(List<ApplicationObject> objects)
        {
            throw new NotImplementedException();
        }

        public void SetConverterSettings(object settings)
        {
            throw new NotImplementedException();
        }

        public void SetPreviousContextObjects(List<ApplicationObject> objects)
        {
            throw new NotImplementedException();
        }

        public RawLamb.Log ConvertToNativeLog(Base @object)
        {
            return null;
        }

        public RawLamb.Board ConvertToNativeBoard(Base @object)
        {
            var board = new Board();
            board.Name = @object["name"] as string;

            var speckle_plane = @object["plane"] as Objects.Geometry.Plane;
            board.Plane = new Plane(
                new Point3d(speckle_plane.origin.x, speckle_plane.origin.y, speckle_plane.origin.z),
                new Vector3d(speckle_plane.xdir.x, speckle_plane.xdir.y, speckle_plane.xdir.z),
                new Vector3d(speckle_plane.ydir.x, speckle_plane.ydir.y, speckle_plane.ydir.z)
                );


            return null;
        }
    }
}
