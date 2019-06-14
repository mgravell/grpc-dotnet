using Grpc.Core;
using ProtoBuf.Grpc.Internal;

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

        AsyncUnaryCall<HelloReply> IGreeter.SayHelloAsync(HelloRequest request, CallOptions options)
           => CallInvoker.AsyncUnaryCall(s_SayHelloAsync, null, options, request);

        //AsyncServerStreamingCall<HelloReply> IGreeter.SayHellos(HelloRequest request, CallOptions options)
        //   => CallInvoker.AsyncServerStreamingCall(s_SayHellosAsync, null, options, request);

        static readonly Method<HelloRequest, HelloReply> s_SayHelloAsync =
            new FullyNamedMethod<HelloRequest, HelloReply>("SayHello", MethodType.Unary, SERVICE_NAME);

        //static readonly Method<HelloRequest, HelloReply> s_SayHellosAsync = new FullyNamedMethod<HelloRequest, HelloReply>(
        //   "SayHellos", MethodType.ServerStreaming, SERVICE_NAME, nameof(IGreeter.SayHellos));
    }
}
