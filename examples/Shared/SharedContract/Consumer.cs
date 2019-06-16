using Grpc.Core;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Internal;
using System.Collections.Generic;
using System.Threading.Tasks;
#pragma warning disable CS0618
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

        ValueTask<HelloReply> IGreeter.SayHelloAsync(HelloRequest request, CallContext context)
            => context.UnaryValueTaskAsync(CallInvoker, s_SayHelloAsync, request);

        IAsyncEnumerable<HelloReply> IGreeter.SayHellos(HelloRequest request, CallContext context)
           => context.ServerStreamingAsync(CallInvoker, s_SayHellosAsync, request);

        static readonly Method<HelloRequest, HelloReply> s_SayHelloAsync =
            new FullyNamedMethod<HelloRequest, HelloReply>("SayHello", MethodType.Unary, SERVICE_NAME);

        static readonly Method<HelloRequest, HelloReply> s_SayHellosAsync = new FullyNamedMethod<HelloRequest, HelloReply>(
           "SayHellos", MethodType.ServerStreaming, SERVICE_NAME, nameof(IGreeter.SayHellos));
    }
}
