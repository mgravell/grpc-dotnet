using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Server.Services
{
    [ServiceContract(Name = "Greet.Greeter")] // only needed to explicitly specify service name
    interface IGreeterService
    {
        CodeFirstGreeterService.HelloReply SayHello(CodeFirstGreeterService.HelloRequest request, ServerCallContext context);
    }

    [ServiceContract(Name = "Greet.Greeter")]
    class CodeFirstGreeterService : IGreeterService // (otherwise, the type's full name is used, i.e. {namespace}.{typename})
    {
        // note: currently only very specific API signatures are supported, as it needs to match
        // the signature that the underlying google API uses; a +1 feature would be to support
        // alternative signatures, for example:
        // a) ValueTask<HelloReply> SayHelloAsync(HelloRequest request) - ValueTask and no context
        // b) HelloReply SayHelloAsync(ServerCallContext context) - sync and no context
        // c) IAsyncEnumerable<HelloReply> SayHellosAsync(HelloRequest request, ServerCallContext context) - IAsyncEnumerable<T>
        // (or is it Channel<T> ?)

        // The tool would generate the corresponding proxy server/client wrapper to make the magic happens
        // In particular, the intention here is that the API *could* be identical between server and client
        // (although that is not a hard requirement or expectation)
        // 

        private readonly ILogger<CodeFirstGreeterService> _logger;

        public CodeFirstGreeterService(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<CodeFirstGreeterService>();
        }

        //public Task<HelloReply> SayHelloAsync(HelloRequest request, ServerCallContext _)
        //{
        //    _logger.LogInformation($"Sending hello to {request.Name}");
        //    return Task.FromResult(new HelloReply { Message = "Hello " + request.Name });
        //}

        HelloReply IGreeterService.SayHello(HelloRequest request, ServerCallContext _)
        {
            _logger.LogInformation($"Sending **sync** hello to {request.Name}");
            return new HelloReply { Message = "Hello (explicit interface impl) " + request.Name };
        }

        [OperationContract]
        public async Task SayHellosAsync(HelloRequest request, IServerStreamWriter<HelloReply> responseStream, ServerCallContext context)
        {
            var httpContext = context.GetHttpContext();
            _logger.LogInformation($"Connection id: {httpContext.Connection.Id}");

            var i = 0;
            while (!context.CancellationToken.IsCancellationRequested)
            {
                var message = $"How are you {request.Name}? {++i}";
                _logger.LogInformation($"Sending greeting {message}.");

                await responseStream.WriteAsync(new HelloReply { Message = message });

                // Gotta look busy
                await Task.Delay(1000);
            }
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
    }
}
