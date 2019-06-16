using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using ProtoBuf.Grpc;
using System;
using Grpc.AspNetCore.Server.Model;
using ProtoBuf.Grpc.Internal;
using System.Threading;
using System.Runtime.CompilerServices;

#pragma warning disable CS0618

namespace SharedContract
{
    public class AllOptionsServer : IAllOptions
    {
        static HelloReply RespondUnary(HelloRequest request, [CallerMemberName] string? caller = null)
            => new HelloReply { Message = $"{nameof(MethodType.Unary)} from {caller}; was {request.Name}" };

        static Task<HelloReply> RespondClientStreaming(IAsyncStreamReader<HelloRequest> request, ServerCallContext context, [CallerMemberName] string? caller = null)
            => RespondClientStreaming(request.AsAsyncEnumerable(context.CancellationToken), caller).AsTask();
        static async ValueTask<HelloReply> RespondClientStreaming(IAsyncEnumerable<HelloRequest> request, [CallerMemberName] string? caller = null)
        {
            string? first = null;
            int count = 0;
            await foreach (var item in request)
            {
                if (count == 0) first = item.Name;
                count++;
            }
            return new HelloReply { Message = $"{nameof(MethodType.ClientStreaming)} from {caller}; first {first}; count {count}" };
        }

        static async IAsyncEnumerable<HelloReply> RespondServerStreaming(HelloRequest request, [EnumeratorCancellation] CancellationToken cancellationToken, [CallerMemberName] string? caller = null)
        {
            yield return new HelloReply { Message = $"{nameof(MethodType.ServerStreaming)} from {caller}; was {request.Name}" };
            await Task.Yield();
            yield return new HelloReply { Message = $"and another {nameof(MethodType.ServerStreaming)}" };
        }

        static async IAsyncEnumerable<HelloReply> RespondDuplex(IAsyncEnumerable<HelloRequest> request, [EnumeratorCancellation] CancellationToken cancellationToken, [CallerMemberName] string? caller = null)
        {
            await foreach(var item in request)
            {
                yield return new HelloReply { Message = $"{nameof(MethodType.DuplexStreaming)} from {caller}; was {item.Name}" };
            }
        }


        // the purpose of this type is to explore the server binding trees
        private static readonly ClientStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Server_ClientStreaming
            = (service, request, context) => ((IAllOptions)service).Server_ClientStreaming(request, context);
        Task<HelloReply> IAllOptions.Server_ClientStreaming(IAsyncStreamReader<HelloRequest> request, ServerCallContext context) => RespondClientStreaming(request, context);

        private static readonly DuplexStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Server_Duplex
            = (service, request, response, context) => ((IAllOptions)service).Server_Duplex(request, response, context);
        Task IAllOptions.Server_Duplex(IAsyncStreamReader<HelloRequest> request, IServerStreamWriter<HelloReply> response, ServerCallContext context) => RespondDuplex(request.AsAsyncEnumerable(context.CancellationToken), context.CancellationToken).WriteTo(response, context.CancellationToken);

        private static readonly ServerStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Server_ServerStreaming
            = (service, request, response, context) => ((IAllOptions)service).Server_ServerStreaming(request, response, context);
        Task IAllOptions.Server_ServerStreaming(HelloRequest request, IServerStreamWriter<HelloReply> response, ServerCallContext context) => RespondServerStreaming(request, context.CancellationToken).WriteTo(response, context.CancellationToken);

        private static readonly UnaryServerMethod<AllOptionsServer, HelloRequest, HelloReply> Server_Unary
            = (service, request, context) => ((IAllOptions)service).Server_Unary(request, context);
        Task<HelloReply> IAllOptions.Server_Unary(HelloRequest request, ServerCallContext context) => Task.FromResult(RespondUnary(request));

        private static readonly UnaryServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_BlockingUnary_Context
            = (service, request, context) => Task.FromResult(((IAllOptions)service).Shared_BlockingUnary_Context(request, new CallContext(context)));
        HelloReply IAllOptions.Shared_BlockingUnary_Context(HelloRequest request, CallContext context) => RespondUnary(request);

        private static readonly UnaryServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_BlockingUnary_NoContext
            = (service, request, context) => Task.FromResult(((IAllOptions)service).Shared_BlockingUnary_NoContext(request));
        HelloReply IAllOptions.Shared_BlockingUnary_NoContext(HelloRequest request) => RespondUnary(request);

        private static readonly DuplexStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_Duplex_Context
            = (service, request, response, context) => ((IAllOptions)service).Shared_Duplex_Context(request.AsAsyncEnumerable(context.CancellationToken), new CallContext(context)).WriteTo(response, context.CancellationToken);
        IAsyncEnumerable<HelloReply> IAllOptions.Shared_Duplex_Context(IAsyncEnumerable<HelloRequest> request, CallContext context) => RespondDuplex(request, context.CancellationToken);

        private static readonly DuplexStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_Duplex_NoContext
            = (service, request, response, context) => ((IAllOptions)service).Shared_Duplex_NoContext(request.AsAsyncEnumerable(context.CancellationToken)).WriteTo(response, context.CancellationToken);
        IAsyncEnumerable<HelloReply> IAllOptions.Shared_Duplex_NoContext(IAsyncEnumerable<HelloRequest> request) => RespondDuplex(request, default);

        private static readonly ServerStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_ServerStreaming_Context
            = (service, request, response, context) => ((IAllOptions)service).Shared_ServerStreaming_Context(request, new CallContext(context)).WriteTo(response, context.CancellationToken);
        IAsyncEnumerable<HelloReply> IAllOptions.Shared_ServerStreaming_Context(HelloRequest request, CallContext context) => RespondServerStreaming(request, context.CancellationToken);

        private static readonly ServerStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_ServerStreaming_NoContext
            = (service, request, response, context) => ((IAllOptions)service).Shared_ServerStreaming_NoContext(request).WriteTo(response, context.CancellationToken);
        IAsyncEnumerable<HelloReply> IAllOptions.Shared_ServerStreaming_NoContext(HelloRequest request) => RespondServerStreaming(request, default);

        private static readonly ClientStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_TaskClientStreaming_Context
            = (service, request, context) => ((IAllOptions)service).Shared_TaskClientStreaming_Context(request.AsAsyncEnumerable(context.CancellationToken), new CallContext(context));
        Task<HelloReply> IAllOptions.Shared_TaskClientStreaming_Context(IAsyncEnumerable<HelloRequest> request, CallContext context) => RespondClientStreaming(request).AsTask();

        private static readonly ClientStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_TaskClientStreaming_NoContext
            = (service, request, context) => ((IAllOptions)service).Shared_TaskClientStreaming_NoContext(request.AsAsyncEnumerable(context.CancellationToken));
        Task<HelloReply> IAllOptions.Shared_TaskClientStreaming_NoContext(IAsyncEnumerable<HelloRequest> request) => RespondClientStreaming(request).AsTask();

        private static readonly UnaryServerMethod<AllOptionsServer, HelloRequest, HelloReply> siShared_TaskUnary_Context
            = (service, request, context) => ((IAllOptions)service).Shared_TaskUnary_Context(request, new CallContext(context));
        Task<HelloReply> IAllOptions.Shared_TaskUnary_Context(HelloRequest request, CallContext context) => Task.FromResult(RespondUnary(request));

        private static readonly UnaryServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_TaskUnary_NoContext
            = (service, request, context) => ((IAllOptions)service).Shared_TaskUnary_NoContext(request);
        Task<HelloReply> IAllOptions.Shared_TaskUnary_NoContext(HelloRequest request) => Task.FromResult(RespondUnary(request));

        private static readonly ClientStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_ValueTaskClientStreaming_Context
            = (service, request, context) => ((IAllOptions)service).Shared_ValueTaskClientStreaming_Context(request.AsAsyncEnumerable(context.CancellationToken), new CallContext(context)).AsTask();
        ValueTask<HelloReply> IAllOptions.Shared_ValueTaskClientStreaming_Context(IAsyncEnumerable<HelloRequest> request, CallContext context) => RespondClientStreaming(request);

        private static readonly ClientStreamingServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_ValueTaskClientStreaming_NoContext
            = (service, request, context) => ((IAllOptions)service).Shared_ValueTaskClientStreaming_NoContext(request.AsAsyncEnumerable(context.CancellationToken)).AsTask();
        ValueTask<HelloReply> IAllOptions.Shared_ValueTaskClientStreaming_NoContext(IAsyncEnumerable<HelloRequest> request) => RespondClientStreaming(request);

        private static readonly UnaryServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_ValueTaskUnary_Context
            = (service, request, context) => ((IAllOptions)service).Shared_ValueTaskUnary_Context(request, new CallContext(context)).AsTask();
        ValueTask<HelloReply> IAllOptions.Shared_ValueTaskUnary_Context(HelloRequest request, CallContext context) => new ValueTask<HelloReply>(RespondUnary(request));

        private static readonly UnaryServerMethod<AllOptionsServer, HelloRequest, HelloReply> Shared_ValueTaskUnary_NoContext
            = (service, request, context) => ((IAllOptions)service).Shared_ValueTaskUnary_NoContext(request).AsTask();
        ValueTask<HelloReply> IAllOptions.Shared_ValueTaskUnary_NoContext(HelloRequest request) => new ValueTask<HelloReply>(RespondUnary(request));

        AsyncUnaryCall<HelloReply> IAllOptions.Client_AsyncUnary(HelloRequest request, CallOptions options) => throw new NotSupportedException();

        HelloReply IAllOptions.Client_BlockingUnary(HelloRequest request, CallOptions options) => throw new NotSupportedException();

        AsyncClientStreamingCall<HelloRequest, HelloReply> IAllOptions.Client_ClientStreaming(CallOptions options) => throw new NotSupportedException();

        AsyncDuplexStreamingCall<HelloRequest, HelloReply> IAllOptions.Client_Duplex(CallOptions options) => throw new NotSupportedException();

        AsyncServerStreamingCall<HelloReply> IAllOptions.Client_ServerStreaming(HelloRequest request, CallOptions options) => throw new NotSupportedException();
    }
}
