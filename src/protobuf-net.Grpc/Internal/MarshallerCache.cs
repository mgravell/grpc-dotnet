using Grpc.Core;
using System.IO;

namespace ProtoBuf.Grpc.Internal
{
    internal static class MarshallerCache<T>
    {
        public static Marshaller<T> Instance { get; }
            = new Marshaller<T>(Serialize, Deserialize);

        private static T Deserialize(byte[] arg)
        {
            using (var ms = new MemoryStream(arg))
            {
                return Serializer.Deserialize<T>(ms);
            }
        }

        private static byte[] Serialize(T arg)
        {
            using (var ms = new MemoryStream())
            {
                Serializer.Serialize(ms, arg);
                return ms.ToArray();
            }
        }
    }
}
