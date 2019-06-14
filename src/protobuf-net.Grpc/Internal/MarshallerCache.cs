using Grpc.Core;
using ProtoBuf.Meta;
using System.IO;

namespace ProtoBuf.Grpc.Internal
{
    internal static class MarshallerCache<T>
    {
        public static Marshaller<T> Instance { get; } = new Marshaller<T>(Serialize, Deserialize);

        private static readonly RuntimeTypeModel _model = RuntimeTypeModel.Default;

        private static T Deserialize(byte[] payload)
        {
            using (var reader = ProtoReader.Create(out var state, payload, _model))
            {
                return (T)_model.Deserialize(reader, ref state, null, typeof(T));
            }
        }

        private static byte[] Serialize(T value)
        {
            using (var ms = new MemoryStream())
            {
                Serializer.Serialize(ms, value);
                return ms.ToArray();
            }
        }
    }
}
