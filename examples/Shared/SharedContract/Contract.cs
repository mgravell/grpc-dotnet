using Grpc.Core;
using ProtoBuf;
using ProtoBuf.Grpc;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;

namespace SharedContract
{
    [ServiceContract(Name = "Greet.Greeter")]
    public interface IGreeter
    {
        // unary
        ValueTask<HelloReply> SayHelloAsync(HelloRequest request, CallContext context = default);

        // server-streaming
        IAsyncEnumerable<HelloReply> SayHellos(HelloRequest request, CallContext options = default);
    }
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
}
