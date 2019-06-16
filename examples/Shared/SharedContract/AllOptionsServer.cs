using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using ProtoBuf.Grpc;
using System;
using Grpc.AspNetCore.Server.Model;
using System.Threading;
using System.Runtime.CompilerServices;

namespace SharedContract
{
    static class Helpers
    {
        public static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(this IAsyncStreamReader<T> reader, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using (reader)
            {
                while (await reader.MoveNext(cancellationToken))
                {
                    yield return reader.Current;
                }
            }
        }

        public static async Task WriteTo<T>(this IAsyncEnumerable<T> reader, IServerStreamWriter<T> writer, ServerCallContext context)
        {
            await using (var iter = reader.GetAsyncEnumerator(context.CancellationToken))
            {
                while (await iter.MoveNextAsync())
                {
                    await writer.WriteAsync(iter.Current);
                }
            }
        }
    }
    class AllOptionsServer : IAllOptions
    {
        // the purpose of this type is to explore the server binding trees
        private static readonly ClientStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Server_ClientStreaming
            = (service, request, context) => ((IAllOptions)service).Server_ClientStreaming(request, context);
        Task<HelloReply> IAllOptions.Server_ClientStreaming(IAsyncStreamReader<HelloRequest> request, ServerCallContext context) => throw new NotImplementedException();

        private static readonly DuplexStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Server_Duplex
            = (service, request, response, context) => ((IAllOptions)service).Server_Duplex(request, response, context);
        Task IAllOptions.Server_Duplex(IAsyncStreamReader<HelloRequest> request, IServerStreamWriter<HelloReply> response, ServerCallContext context) => throw new NotImplementedException();

        private static readonly ServerStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Server_ServerStreaming
            = (service, request, response, context) => ((IAllOptions)service).Server_ServerStreaming(request, response, context);
        Task IAllOptions.Server_ServerStreaming(HelloRequest request, IServerStreamWriter<HelloReply> response, ServerCallContext context) => throw new NotImplementedException();

        private static readonly UnaryServerMethod<AllOptionsServer, HelloRequest, HelloReply> Server_Unary
            = (service, request, context) => ((IAllOptions)service).Server_Unary(request, context);
        Task<HelloReply> IAllOptions.Server_Unary(HelloRequest request, ServerCallContext context) => throw new NotImplementedException();

        private static readonly UnaryServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_BlockingUnary_Context
            = (service, request, context) => Task.FromResult(((IAllOptions)service).Shared_BlockingUnary_Context(request, new CallContext(context)));
        HelloReply IAllOptions.Shared_BlockingUnary_Context(HelloRequest request, CallContext context) => throw new NotImplementedException();

        private static readonly UnaryServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_BlockingUnary_NoContext
            = (service, request, context) => Task.FromResult(((IAllOptions)service).Shared_BlockingUnary_NoContext(request));
        HelloReply IAllOptions.Shared_BlockingUnary_NoContext(HelloRequest request) => throw new NotImplementedException();

        private static readonly DuplexStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_Duplex_Context
            = (service, request, response, context) => ((IAllOptions)service).Shared_Duplex_Context(request.AsAsyncEnumerable(context.CancellationToken), new CallContext(context)).WriteTo(response, context);
        IAsyncEnumerable<HelloReply> IAllOptions.Shared_Duplex_Context(IAsyncEnumerable<HelloRequest> request, CallContext context) => throw new NotImplementedException();

        private static readonly DuplexStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_Duplex_NoContext
            = (service, request, response, context) => ((IAllOptions)service).Shared_Duplex_NoContext(request.AsAsyncEnumerable(context.CancellationToken)).WriteTo(response, context);
        IAsyncEnumerable<HelloReply> IAllOptions.Shared_Duplex_NoContext(IAsyncEnumerable<HelloRequest> request) => throw new NotImplementedException();

        private static readonly ServerStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_ServerStreaming_Context
            = (service, request, response, context) => ((IAllOptions)service).Shared_ServerStreaming_Context(request, new CallContext(context)).WriteTo(response, context);
        IAsyncEnumerable<HelloReply> IAllOptions.Shared_ServerStreaming_Context(HelloRequest request, CallContext context) => throw new NotImplementedException();

        private static readonly ServerStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_ServerStreaming_NoContext
            = (service, request, response, context) => ((IAllOptions)service).Shared_ServerStreaming_NoContext(request).WriteTo(response, context);
        IAsyncEnumerable<HelloReply> IAllOptions.Shared_ServerStreaming_NoContext(HelloRequest request) => throw new NotImplementedException();

        private static readonly ClientStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_TaskClientStreaming_Context
            = (service, request, context) => ((IAllOptions)service).Shared_TaskClientStreaming_Context(request.AsAsyncEnumerable(context.CancellationToken), new CallContext(context));
        Task<HelloReply> IAllOptions.Shared_TaskClientStreaming_Context(IAsyncEnumerable<HelloRequest> request, CallContext context) => throw new NotImplementedException();

        private static readonly ClientStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_TaskClientStreaming_NoContext
            = (service, request, context) => ((IAllOptions)service).Shared_TaskClientStreaming_NoContext(request.AsAsyncEnumerable(context.CancellationToken));
        Task<HelloReply> IAllOptions.Shared_TaskClientStreaming_NoContext(IAsyncEnumerable<HelloRequest> request) => throw new NotImplementedException();

        private static readonly UnaryServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_TaskUnary_Context
            = (service, request, context) => ((IAllOptions)service).Shared_TaskUnary_Context(request, new CallContext(context));
        Task<HelloReply> IAllOptions.Shared_TaskUnary_Context(HelloRequest request, CallContext context) => throw new NotImplementedException();

        private static readonly UnaryServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_TaskUnary_NoContext
            = (service, request, context) => ((IAllOptions)service).Shared_TaskUnary_NoContext(request);
        Task<HelloReply> IAllOptions.Shared_TaskUnary_NoContext(HelloRequest request) => throw new NotImplementedException();

        private static readonly ClientStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_ValueTaskClientStreaming_Context
            = (service, request, context) => ((IAllOptions)service).Shared_ValueTaskClientStreaming_Context(request.AsAsyncEnumerable(context.CancellationToken), new CallContext(context)).AsTask();
        ValueTask<HelloReply> IAllOptions.Shared_ValueTaskClientStreaming_Context(IAsyncEnumerable<HelloRequest> request, CallContext context) => throw new NotImplementedException();

        private static readonly ClientStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_ValueTaskClientStreaming_NoContext
            = (service, request, context) => ((IAllOptions)service).Shared_ValueTaskClientStreaming_NoContext(request.AsAsyncEnumerable(context.CancellationToken)).AsTask();
        ValueTask<HelloReply> IAllOptions.Shared_ValueTaskClientStreaming_NoContext(IAsyncEnumerable<HelloRequest> request) => throw new NotImplementedException();

        private static readonly UnaryServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_ValueTaskUnary_Context
            = (service, request, context) => ((IAllOptions)service).Shared_ValueTaskUnary_Context(request, new CallContext(context)).AsTask();
        ValueTask<HelloReply> IAllOptions.Shared_ValueTaskUnary_Context(HelloRequest request, CallContext context) => throw new NotImplementedException();

        private static readonly UnaryServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_ValueTaskUnary_NoContext
            = (service, request, context) => ((IAllOptions)service).Shared_ValueTaskUnary_NoContext(request).AsTask();
        ValueTask<HelloReply> IAllOptions.Shared_ValueTaskUnary_NoContext(HelloRequest request) => throw new NotImplementedException();

        AsyncUnaryCall<HelloReply> IAllOptions.Client_AsyncUnary(HelloRequest request, CallOptions options) => throw new NotSupportedException();

        HelloReply IAllOptions.Client_BlockingUnary(HelloRequest request, CallOptions options) => throw new NotSupportedException();

        AsyncClientStreamingCall<HelloRequest, HelloReply> IAllOptions.Client_ClientStreaming(CallOptions options) => throw new NotSupportedException();

        AsyncDuplexStreamingCall<HelloRequest, HelloReply> IAllOptions.Client_Duplex(CallOptions options) => throw new NotSupportedException();

        AsyncServerStreamingCall<HelloReply> IAllOptions.Client_ServerStreaming(HelloRequest request, CallOptions options) => throw new NotSupportedException();
    }
}
