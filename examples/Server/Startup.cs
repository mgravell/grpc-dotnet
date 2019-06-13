﻿#region Copyright notice and license

// Copyright 2019 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System;
using System.ServiceModel;
using System.Threading.Tasks;
using Grpc.Core;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Count;
using Greet;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ProtoBuf;
using ProtoBuf.Grpc.Server;
using Microsoft.IdentityModel.Tokens;
using Server.Interceptors;
using Microsoft.Extensions.Logging;

namespace GRPCServer
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddGrpc(options =>
                {
                    // This registers a global interceptor with a Singleton lifetime. The interceptor must be added to the service collection in addition to being registered here.
                    options.Interceptors.Add<MaxConcurrentCallsInterceptor>();
                    // This registers a global interceptor with a Scoped lifetime.
                    options.Interceptors.Add<MaxStreamingRequestTimeoutInterceptor>(TimeSpan.FromSeconds(30));
                })                
                .AddServiceOptions<GreeterService>(options =>
                {
                    // This registers an interceptor for the Greeter service with a Singleton lifetime.
                    // NOTE: Not all calls should be cached. Since the response of this service only depends on the request and no other state, adding caching here is acceptable.
                    options.Interceptors.Add<UnaryCachingInterceptor>();
                });
            services.AddCodeFirstGrpc();
            services.AddGrpcReflection();
            services.AddSingleton(new MaxConcurrentCallsInterceptor(200));
            services.AddSingleton<UnaryCachingInterceptor>();
            services.AddSingleton<IncrementingCounter>();
            services.AddSingleton<MailQueueRepository>();
            services.AddSingleton<TicketRepository>();

            // These clients will call back to the server
            services
                .AddGrpcClient<Greeter.GreeterClient>((s, o) => { o.BaseAddress = GetCurrentAddress(s); })
                .EnableCallContextPropagation();
            services
                .AddGrpcClient<Counter.CounterClient>((s, o) => { o.BaseAddress = GetCurrentAddress(s); })
                .EnableCallContextPropagation();

            services.AddAuthorization(options =>
            {
                options.AddPolicy(JwtBearerDefaults.AuthenticationScheme, policy =>
                {
                    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                    policy.RequireClaim(ClaimTypes.Name);
                });
            });
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters =
                        new TokenValidationParameters
                        {
                            ValidateAudience = false,
                            ValidateIssuer = false,
                            ValidateActor = false,
                            ValidateLifetime = true,
                            IssuerSigningKey = SecurityKey
                        };
                });

            static Uri GetCurrentAddress(IServiceProvider serviceProvider)
            {
                // Get the address of the current server from the request
                var context = serviceProvider.GetRequiredService<IHttpContextAccessor>()?.HttpContext;
                if (context == null)
                {
                    throw new InvalidOperationException("Could not get HttpContext.");
                }

                return new Uri($"{context.Request.Scheme}://{context.Request.Host.Value}");
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<MailerService>();
                endpoints.MapGrpcService<CounterService>();
                // endpoints.MapGrpcService<GreeterService>();
                endpoints.MapGrpcService<CodeFirstGreeterService>();
                endpoints.MapGrpcService<TicketerService>();
                endpoints.MapGrpcService<CertifierService>();
                endpoints.MapGrpcService<AggregatorService>();
                endpoints.MapGrpcReflectionService();

                endpoints.MapGet("/generateJwtToken", context =>
                {
                    return context.Response.WriteAsync(GenerateJwtToken(context.Request.Query["name"]));
                });
            });
        }

        private string GenerateJwtToken(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException("Name is not specified.");
            }

            var claims = new[] { new Claim(ClaimTypes.Name, name) };
            var credentials = new SigningCredentials(SecurityKey, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken("ExampleServer", "ExampleClients", claims, expires: DateTime.Now.AddSeconds(60), signingCredentials: credentials);
            return JwtTokenHandler.WriteToken(token);
        }

        private readonly JwtSecurityTokenHandler JwtTokenHandler = new JwtSecurityTokenHandler();
        private readonly SymmetricSecurityKey SecurityKey = new SymmetricSecurityKey(Guid.NewGuid().ToByteArray());
    }
}

[ServiceContract(Name = "Greet.Greeter")] // only needed to explicitly specify service name
class CodeFirstGreeterService // (otherwise, the type's full name is used, i.e. {namespace}.{typename})
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

    private readonly ILogger _logger;

    public CodeFirstGreeterService(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<CodeFirstGreeterService>();
    }

    //public Task<HelloReply> SayHelloAsync(HelloRequest request, ServerCallContext _)
    //{
    //    _logger.LogInformation($"Sending hello to {request.Name}");
    //    return Task.FromResult(new HelloReply { Message = "Hello " + request.Name });
    //}

    public HelloReply SayHello(HelloRequest request, ServerCallContext _)
    {
        _logger.LogInformation($"Sending **sync** hello to {request.Name}");
        return new HelloReply { Message = "Hello " + request.Name };
    }

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