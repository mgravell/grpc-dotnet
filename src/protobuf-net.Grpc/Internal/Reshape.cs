using Grpc.Core;
using System;
using System.Threading.Tasks;
namespace ProtoBuf.Grpc.Internal
{
    [Obsolete("This class is intended for use by runtime-generated code; all methods can be changed without notice - it is only guaranteed to work with the internally generated code", false)]
    public static class Reshape
    {
        public static async Task<T> AsTask<T>(this AsyncUnaryCall<T> call, MetadataContext? context = null)
        {
            using (call)
            {
                if (context != null) context.Headers = await call.ResponseHeadersAsync;
                var value = await call;
                if (context != null) context.Trailers = call.GetTrailers();
                return value;
            }
        }

        // we expect this to be async, so no point lying about it - we'll have a Task
        public static ValueTask<T> AsValueTask<T>(this AsyncUnaryCall<T> call, MetadataContext? context = null) => new ValueTask<T>(AsTask<T>(call, context));

        public static T AsSync<T>(this AsyncUnaryCall<T> call, MetadataContext? context = null) => AsTask<T>(call, context).Result; // not nice, but it works; TODO: migrate to blocking

        // can't have this "for real" until the RX (System.Interactive.Async) thing is unified, in particular
        // Internal\Reshape.cs(27,29): error CS0433: The type 'IAsyncEnumerable<T>' exists in both 'System.Interactive.Async, Version=3.2.0.0, Culture=neutral, PublicKeyToken=94bc3704cddfc263' and 'System.Runtime, Version=4.2.1.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' [C:\Code\grpc-dotnet\src\protobuf-net.Grpc\protobuf-net.Grpc.csproj]
        // (via Grpc.Core taking a dep on System.Interactive.Async  3.2.0 - needs to update to 4.*, but that is alpha because System.Runtime is alpha)
        //public static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(AsyncServerStreamingCall<T> value)
        //{
        //    using (value)
        //    {
        //        var seq = value.ResponseStream;
        //        while (await seq.MoveNextAsync())
        //        {
        //            yield return seq.Current;
        //        }
        //    }
        //}
    }
}
