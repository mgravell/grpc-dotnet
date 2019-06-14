using Grpc.Core;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Internal;
using System.Threading.Tasks;

namespace SharedContract
{
    //this is approximately what we want to emit
    public sealed class ManualGreeterClient : ClientBase, IGreeter
    {

        public ManualGreeterClient(CallInvoker callInvoker) : base(callInvoker) { } // this is the one used by GrpcClient
        private ManualGreeterClient(ClientBaseConfiguration configuration) : base(configuration) { }
        private ManualGreeterClient() : base() { }
        public ManualGreeterClient(Channel channel) : base(channel) { }

        private const string SERVICE_NAME = "Greet.Greeter";
        public override string ToString() => SERVICE_NAME;
#pragma warning disable CS0618
        // ValueTask<HelloReply> IGreeter.SayHelloAsync(HelloRequest request)
        //  => Reshape.AsValueTask<HelloReply>(CallInvoker.AsyncUnaryCall(s_SayHelloAsync, null, default, request));

        ValueTask<HelloReply> IGreeter.SayHelloAsync(HelloRequest request, CallContext context)
            => Reshape.AsValueTask<HelloReply>(CallInvoker.AsyncUnaryCall(s_SayHelloAsync, null, context, request));

        //
        //        ValueTask<HelloReply> IGreeter.SayHelloAsync(HelloRequest request, CallOptions options)
        //            => Reshape.AsValueTask(CallInvoker.AsyncUnaryCall(s_SayHelloAsync, null, options, request));
#pragma warning restore CS0618

        //AsyncServerStreamingCall<HelloReply> IGreeter.SayHellos(HelloRequest request, CallOptions options)
        //   => CallInvoker.AsyncServerStreamingCall(s_SayHellosAsync, null, options, request);

        static readonly Method<HelloRequest, HelloReply> s_SayHelloAsync =
            new FullyNamedMethod<HelloRequest, HelloReply>("SayHello", MethodType.Unary, SERVICE_NAME);

        //static readonly Method<HelloRequest, HelloReply> s_SayHellosAsync = new FullyNamedMethod<HelloRequest, HelloReply>(
        //   "SayHellos", MethodType.ServerStreaming, SERVICE_NAME, nameof(IGreeter.SayHellos));
    }
}
