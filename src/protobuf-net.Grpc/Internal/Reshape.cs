using Grpc.Core;
using System;
using System.Collections.Generic;
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
                if (context != null)
                {
                    context.StatusProvider = call;
                    context.Headers = await call.ResponseHeadersAsync;
                }
                var value = await call;
                if (context != null) context.Trailers = call.GetTrailers();
                return value;
            }
        }

        // we expect this to be async, so no point lying about it - we'll have a Task
        public static ValueTask<T> AsValueTask<T>(this AsyncUnaryCall<T> call, MetadataContext? context = null) => new ValueTask<T>(AsTask<T>(call, context));

        public static T AsSync<T>(this AsyncUnaryCall<T> call, MetadataContext? context = null) => AsTask<T>(call, context).Result; // not nice, but it works; TODO: migrate to blocking

        public static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(AsyncServerStreamingCall<T> call, MetadataContext? context = null)
        {
            using (call)
            {
                if (context != null)
                {
                    context.StatusProvider = call;
                    context.Headers = await call.ResponseHeadersAsync;
                }
                using (var seq = call.ResponseStream)
                {
                    while (await seq.MoveNext(default))
                    {
                        yield return seq.Current;
                    }
                    if (context != null) call.GetTrailers();
                }
            }
        }
    }
}
