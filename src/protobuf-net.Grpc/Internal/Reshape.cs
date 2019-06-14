using Grpc.Core;
using System;
using System.Threading.Tasks;
namespace ProtoBuf.Grpc.Internal
{
    [Obsolete("This class is intended for use by runtime-generated code; all methods can be changed without notice - it is only guaranteed to work with the internally generated code", false)]
    public static class Reshape
    {
        public static async Task<T> AsTask<T>(AsyncUnaryCall<T> value)
        {
            using (value)
            {
                return await value;
            }
        }
        public static async ValueTask<T> AsValueTask<T>(AsyncUnaryCall<T> value)
        {
            using (value)
            {
                return await value;
            }
        }
        public static T AsSync<T>(AsyncUnaryCall<T> value) => AsTask<T>(value).Result; // not nice, but it works

        // can't have this "for real" until the RX (System.Interactive.Async) thing is unified, in particular
        // Internal\Reshape.cs(27,29): error CS0433: The type 'IAsyncEnumerable<T>' exists in both 'System.Interactive.Async, Version=3.2.0.0, Culture=neutral, PublicKeyToken=94bc3704cddfc263' and 'System.Runtime, Version=4.2.1.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' [C:\Code\grpc-dotnet\src\protobuf-net.Grpc\protobuf-net.Grpc.csproj]
        // (via Grpc.Core taking a dep on System.Interactive.Async  3.2.0 - needs to update to 4.*, but that is alpha because System.Runtime is alpha)
        //public static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(AsyncServerStreamingCall<T> value)
        //{
        //    using (value)
        //    {
        //        var seq = value.ResponseStream;
        //        while (await seq.MoveNext())
        //        {
        //            yield return seq.Current;
        //        }
        //    }
        //}
    }
}
