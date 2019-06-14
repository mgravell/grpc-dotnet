using Grpc.Core;
using ProtoBuf;
using ProtoBuf.Grpc;
using System.ServiceModel;
using System.Threading.Tasks;

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

    [ServiceContract(Name = "Greet.Greeter")]
    public interface IGreeter
    {
        ValueTask<HelloReply> SayHelloAsync(HelloRequest request, CallContext context = default);

        //AsyncServerStreamingCall<HelloReply> SayHellos(HelloRequest request, CallOptions options = default);
    }
}
