using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Server.Services
{
    [ServiceContract(Name = "Greet.Greeter")]
    interface IGreeterService
    {
        CodeFirstGreeterService.HelloReply SayHello(CodeFirstGreeterService.HelloRequest request, ServerCallContext context);
    }

    [ServiceContract(Name = "Greet.Greeter")]
    class CodeFirstGreeterService : IGreeterService
    {

        private readonly ILogger<CodeFirstGreeterService> _logger;

        public CodeFirstGreeterService(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<CodeFirstGreeterService>();
        }

        HelloReply IGreeterService.SayHello(HelloRequest request, ServerCallContext _)
        {
            _logger.LogInformation($"Sending hello to {request.Name}");
            return new HelloReply { Message = "Hello (sync/explicit interface impl) " + request.Name };
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
