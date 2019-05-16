using Grpc.Core;
using ProtoBuf.Grpc.Internal;

namespace SharedContract
{
    //static class Consumer
    //{
    //    static async Task TheirCode()
    //    {
    //        var channel = new Channel("localhost:50051", ChannelCredentials.Insecure);

    //        var client = ClientFactory.CreateClient<IMyService>(channel);

    //        HelloReply response = await client.SayHelloAsync(new HelloRequest { Name = "abc" });
    //        Console.WriteLine(response.Message);

    //    }
    //}
    public static class ClientFactory
    {
        public static T CreateClient<T>(Channel channel) where T : class
        {
            if (typeof(T) == typeof(IGreeter))
            {
                ClientBase client = new GreeterClient(channel);
                return (T)(object)(client);
            }
            return ProtoBuf.Grpc.Client.ClientFactory.CreateClient<T>(channel);
        }
    }
     //this is approximately what we want to emit
    internal sealed class GreeterClient : ClientBase, IGreeter
    {
        internal GreeterClient(Channel channel) : base(channel) { }

        private const string SERVICE_NAME = "Greet.Greeter";
        public override string ToString() => SERVICE_NAME;

        AsyncUnaryCall<HelloReply> IGreeter.SayHello(HelloRequest request, CallOptions options)
           => CallInvoker.AsyncUnaryCall(s_SayHelloAsync, null, options, request);

        AsyncServerStreamingCall<HelloReply> IGreeter.SayHellos(HelloRequest request, CallOptions options)
           => CallInvoker.AsyncServerStreamingCall(s_SayHellosAsync, null, options, request);

        static readonly Method<HelloRequest, HelloReply> s_SayHelloAsync = new FullyNamedMethod<HelloRequest, HelloReply>(
           "SayHello", MethodType.Unary, SERVICE_NAME, nameof(IGreeter.SayHello));

        static readonly Method<HelloRequest, HelloReply> s_SayHellosAsync = new FullyNamedMethod<HelloRequest, HelloReply>(
           "SayHellos", MethodType.ServerStreaming, SERVICE_NAME, nameof(IGreeter.SayHellos));
    }
}
