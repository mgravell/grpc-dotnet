using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Internal;

#pragma warning disable CS0618
namespace SharedContract
{
    public class AllOptionsClient : ClientBase, IAllOptions
    {
        const string SERVICE_NAME = nameof(IAllOptions);
        public AsyncUnaryCall<HelloReply> Client_AsyncUnary(HelloRequest request, CallOptions options)
            => CallInvoker.AsyncUnaryCall<HelloRequest, HelloReply>(s_Client_AsyncUnary, null, options, request);
        static readonly Method<HelloRequest, HelloReply> s_Client_AsyncUnary = new FullyNamedMethod<HelloRequest, HelloReply>(nameof(Client_AsyncUnary), MethodType.Unary, SERVICE_NAME);

        public HelloReply Client_BlockingUnary(HelloRequest request, CallOptions options)
            => CallInvoker.BlockingUnaryCall<HelloRequest, HelloReply>(s_Client_BlockingUnary, null, options, request);
        static readonly Method<HelloRequest, HelloReply> s_Client_BlockingUnary = new FullyNamedMethod<HelloRequest, HelloReply>(nameof(Client_BlockingUnary), MethodType.Unary, SERVICE_NAME);

        public AsyncClientStreamingCall<HelloRequest, HelloReply> Client_ClientStreaming(CallOptions options)
            => CallInvoker.AsyncClientStreamingCall<HelloRequest, HelloReply>(s_Client_ClientStreaming, null, options);
        static readonly Method<HelloRequest, HelloReply> s_Client_ClientStreaming = new FullyNamedMethod<HelloRequest, HelloReply>(nameof(Client_ClientStreaming), MethodType.ClientStreaming, SERVICE_NAME);

        public AsyncDuplexStreamingCall<HelloRequest, HelloReply> Client_Duplex(CallOptions options)
            => CallInvoker.AsyncDuplexStreamingCall<HelloRequest, HelloReply>(s_Client_Duplex, null, options);
        static readonly Method<HelloRequest, HelloReply> s_Client_Duplex = new FullyNamedMethod<HelloRequest, HelloReply>(nameof(Client_Duplex), MethodType.DuplexStreaming, SERVICE_NAME);

        public AsyncServerStreamingCall<HelloReply> Client_ServerStreaming(HelloRequest request, CallOptions options)
            => CallInvoker.AsyncServerStreamingCall<HelloRequest, HelloReply>(s_Client_ServerStreaming, null, options, request);
        static readonly Method<HelloRequest, HelloReply> s_Client_ServerStreaming = new FullyNamedMethod<HelloRequest, HelloReply>(nameof(Client_ServerStreaming), MethodType.ServerStreaming, SERVICE_NAME);


        public Task<HelloReply> Server_ClientStreaming(IAsyncStreamReader<HelloRequest> request, ServerCallContext context) => throw new NotSupportedException();

        public Task Server_Duplex(IAsyncStreamReader<HelloRequest> request, IServerStreamWriter<HelloReply> response, ServerCallContext context) => throw new NotSupportedException();

        public Task Server_ServerStreaming(HelloRequest request, IServerStreamWriter<HelloReply> response, ServerCallContext context) => throw new NotSupportedException();

        public Task<HelloReply> Server_Unary(HelloRequest request, ServerCallContext context) => throw new NotSupportedException();



        public HelloReply Shared_BlockingUnary_Context(HelloRequest request, CallContext context)
            => CallInvoker.BlockingUnaryCall<HelloRequest, HelloReply>(s_Shared_BlockingUnary_Context, null, context.Client, request);
        static readonly Method<HelloRequest, HelloReply> s_Shared_BlockingUnary_Context = new FullyNamedMethod<HelloRequest, HelloReply>(nameof(Shared_BlockingUnary_Context), MethodType.Unary, SERVICE_NAME);

        public HelloReply Shared_BlockingUnary_NoContext(HelloRequest request)
            => CallInvoker.BlockingUnaryCall<HelloRequest, HelloReply>(s_Shared_BlockingUnary_NoContext, null, default, request);
        static readonly Method<HelloRequest, HelloReply> s_Shared_BlockingUnary_NoContext = new FullyNamedMethod<HelloRequest, HelloReply>(nameof(Shared_BlockingUnary_NoContext), MethodType.Unary, SERVICE_NAME);


        public IAsyncEnumerable<HelloReply> Shared_Duplex_Context(IAsyncEnumerable<HelloRequest> request, CallContext context)
            => context.DuplexAsync(CallInvoker, s_Shared_Duplex_Context, request);

        static readonly Method<HelloRequest, HelloReply> s_Shared_Duplex_Context = new FullyNamedMethod<HelloRequest, HelloReply>(nameof(Shared_Duplex_Context), MethodType.DuplexStreaming, SERVICE_NAME);

        public IAsyncEnumerable<HelloReply> Shared_Duplex_NoContext(IAsyncEnumerable<HelloRequest> request)
            => CallContext.Default.DuplexAsync(CallInvoker, s_Shared_Duplex_NoContext, request);

        static readonly Method<HelloRequest, HelloReply> s_Shared_Duplex_NoContext = new FullyNamedMethod<HelloRequest, HelloReply>(nameof(Shared_Duplex_NoContext), MethodType.DuplexStreaming, SERVICE_NAME);

        public IAsyncEnumerable<HelloReply> Shared_ServerStreaming_Context(HelloRequest request, CallContext context)
            => context.ServerStreamingAsync(CallInvoker, s_Shared_ServerStreaming_Context, request);
        static readonly Method<HelloRequest, HelloReply> s_Shared_ServerStreaming_Context = new FullyNamedMethod<HelloRequest, HelloReply>(nameof(Shared_ServerStreaming_Context), MethodType.ServerStreaming, SERVICE_NAME);

        public IAsyncEnumerable<HelloReply> Shared_ServerStreaming_NoContext(HelloRequest request)
            => CallContext.Default.ServerStreamingAsync(CallInvoker, s_Shared_ServerStreaming_NoContext, request);
        static readonly Method<HelloRequest, HelloReply> s_Shared_ServerStreaming_NoContext = new FullyNamedMethod<HelloRequest, HelloReply>(nameof(Shared_ServerStreaming_NoContext), MethodType.ServerStreaming, SERVICE_NAME);

        public Task<HelloReply> Shared_TaskClientStreaming_Context(IAsyncEnumerable<HelloRequest> request, CallContext context)
            => context.ClientStreamingTaskAsync(CallInvoker, s_Shared_TaskClientStreaming_Context, request);
        static readonly Method<HelloRequest, HelloReply> s_Shared_TaskClientStreaming_Context = new FullyNamedMethod<HelloRequest, HelloReply>(nameof(Shared_TaskClientStreaming_Context), MethodType.ClientStreaming, SERVICE_NAME);

        public Task<HelloReply> Shared_TaskClientStreaming_NoContext(IAsyncEnumerable<HelloRequest> request)
            => CallContext.Default.ClientStreamingTaskAsync(CallInvoker, s_Shared_TaskClientStreaming_NoContext, request);
        static readonly Method<HelloRequest, HelloReply> s_Shared_TaskClientStreaming_NoContext = new FullyNamedMethod<HelloRequest, HelloReply>(nameof(Shared_TaskClientStreaming_NoContext), MethodType.ClientStreaming, SERVICE_NAME);

        public Task<HelloReply> Shared_TaskUnary_Context(HelloRequest request, CallContext context)
            => context.UnaryTaskAsync(CallInvoker, s_Shared_TaskUnary_Context, request);
        static readonly Method<HelloRequest, HelloReply> s_Shared_TaskUnary_Context = new FullyNamedMethod<HelloRequest, HelloReply>(nameof(Shared_TaskUnary_Context), MethodType.Unary, SERVICE_NAME);

        public Task<HelloReply> Shared_TaskUnary_NoContext(HelloRequest request)
            => CallContext.Default.UnaryTaskAsync(CallInvoker, s_Shared_TaskUnary_NoContext, request);
        static readonly Method<HelloRequest, HelloReply> s_Shared_TaskUnary_NoContext = new FullyNamedMethod<HelloRequest, HelloReply>(nameof(Shared_TaskUnary_NoContext), MethodType.Unary, SERVICE_NAME);

        public ValueTask<HelloReply> Shared_ValueTaskClientStreaming_Context(IAsyncEnumerable<HelloRequest> request, CallContext context)
            => context.ClientStreamingValueTaskAsync(CallInvoker, s_Shared_ValueTaskClientStreaming_Context, request);
        static readonly Method<HelloRequest, HelloReply> s_Shared_ValueTaskClientStreaming_Context = new FullyNamedMethod<HelloRequest, HelloReply>(nameof(Shared_ValueTaskClientStreaming_Context), MethodType.ClientStreaming, SERVICE_NAME);

        public ValueTask<HelloReply> Shared_ValueTaskClientStreaming_NoContext(IAsyncEnumerable<HelloRequest> request)
            => CallContext.Default.ClientStreamingValueTaskAsync(CallInvoker, s_Shared_ValueTaskClientStreaming_NoContext, request);
        static readonly Method<HelloRequest, HelloReply> s_Shared_ValueTaskClientStreaming_NoContext = new FullyNamedMethod<HelloRequest, HelloReply>(nameof(Shared_ValueTaskClientStreaming_NoContext), MethodType.ClientStreaming, SERVICE_NAME);

        public ValueTask<HelloReply> Shared_ValueTaskUnary_Context(HelloRequest request, CallContext context)
            => context.UnaryValueTaskAsync(CallInvoker, s_Shared_ValueTaskUnary_Context, request);
        static readonly Method<HelloRequest, HelloReply> s_Shared_ValueTaskUnary_Context = new FullyNamedMethod<HelloRequest, HelloReply>(nameof(Shared_ValueTaskUnary_Context), MethodType.Unary, SERVICE_NAME);

        public ValueTask<HelloReply> Shared_ValueTaskUnary_NoContext(HelloRequest request)
            => CallContext.Default.UnaryValueTaskAsync(CallInvoker, s_Shared_ValueTaskUnary_NoContext, request);
        static readonly Method<HelloRequest, HelloReply> s_Shared_ValueTaskUnary_NoContext = new FullyNamedMethod<HelloRequest, HelloReply>(nameof(Shared_ValueTaskUnary_NoContext), MethodType.Unary, SERVICE_NAME);
    }
}
