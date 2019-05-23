using Grpc.Core;
using ProtoBuf;
using System.ServiceModel;

namespace SharedContract
{
    [ProtoContract]
    public class HelloRequest
    {
        [ProtoMember(1)]
        public string? Name { get; set; }
    }
    [ProtoContract]
    public class HelloReply
    {
        [ProtoMember(1)]
        public string? Message { get; set; }
    }

    [ServiceContract(Name = "whatever")]
    interface IMyService
    {
        AsyncUnaryCall<HelloReply> SayHelloAsync(HelloRequest request, CallOptions options = default);

        // alternative APIs to recognize and support?
        // Task<HelloReply> SayHelloAsync(HelloRequest request);
        // Task<HelloReply> SayHelloAsync(CancellationToken token);
    }

    public interface IGreeter
    {
        // this is the inital version that assumes same client API as google
        AsyncUnaryCall<HelloReply> SayHello(HelloRequest request, CallOptions options = default);
        AsyncServerStreamingCall<HelloReply> SayHellos(HelloRequest request, CallOptions options = default);
    }
}
