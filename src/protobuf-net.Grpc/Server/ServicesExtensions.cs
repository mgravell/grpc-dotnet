using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Server
{
    public static class ServicesExtensions
    {
        public static void AddCodeFirstGrpc(this IServiceCollection services)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IServiceMethodProvider<>), typeof(CodeFirstServiceMethodProvider<>)));
        }

        private sealed class CodeFirstServiceMethodProvider<TService> : IServiceMethodProvider<TService> where TService : class
        {
            private readonly ILogger<CodeFirstServiceMethodProvider<TService>> _logger;

            public CodeFirstServiceMethodProvider(ILoggerFactory loggerFactory)
            {
                _logger = _logger = loggerFactory.CreateLogger<CodeFirstServiceMethodProvider<TService>>();
            }
            public void OnServiceMethodDiscovery(ServiceMethodProviderContext<TService> context)
            {
                // ignore any services that are known to be the default handler
                if (Attribute.IsDefined(typeof(TService), typeof(BindServiceMethodAttribute))) return;

                // we support methods that match suitable signatures, where:
                // - the method is directly on TService and is marked [OperationContract]
                // - the method is on an interface that TService implements, and the interface is marked [ServiceContract]
                AddMethodsForService(context,typeof(TService));
                foreach(var iType in typeof(TService).GetInterfaces())
                {
                    AddMethodsForService(context, iType);
                }
            }

            private void AddMethodsForService(ServiceMethodProviderContext<TService> context, Type serviceContract)
            {
                bool isPublicContract = typeof(TService) == serviceContract;
                var sva = (ServiceContractAttribute?)Attribute.GetCustomAttribute(serviceContract, typeof(ServiceContractAttribute), inherit: true);
                if (sva == null && !isPublicContract) return; // for interfaces: only process those marked [ServiceContract]; for the class itself: this is optional

                _logger.Log(LogLevel.Warning, "pb-net processing {0}/{1}", typeof(TService).Name, serviceContract.Name);

                var serviceName = sva?.Name;
                if (string.IsNullOrWhiteSpace(serviceName)) serviceName = serviceContract.FullName?.Replace('+', '.');
                if (string.IsNullOrWhiteSpace(serviceName)) return; // no shirt, no shoes, no service

                #region Utility method to invoke AddMethod<,,>
                object?[]? argsBuffer = null;
                Type[] typesBuffer = Array.Empty<Type>();
                bool AddMethod(Type @in, Type @out, MethodInfo m, MethodType t, Func<ParameterExpression[], Expression>? invoker = null)
                {
                    if (typesBuffer.Length == 0)
                    {
                        typesBuffer = new Type[] { typeof(TService), typeof(void), typeof(void) };
                    }
                    typesBuffer[1] = @in;
                    typesBuffer[2] = @out;

                    if (argsBuffer == null)
                    {
                        argsBuffer = new object?[] { serviceName, null, null, context, _logger, null };
                    }
                    argsBuffer[1] = m;
                    argsBuffer[2] = t;
                    argsBuffer[5] = invoker;

                    s_addMethod.MakeGenericMethod(typesBuffer).Invoke(null, argsBuffer);
                    return true;
                }

                #endregion

                foreach (var method in serviceContract.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    try
                    {
                        if (isPublicContract && !Attribute.IsDefined(method, typeof(OperationContractAttribute)))
                            continue; // for methods on the class (not a service contract interface): demand [OperationContract]

                        var outType = method.ReturnType;
                        if (outType == null) continue;

                        bool bound = false;
                        var args = method.GetParameters();
                        if (args.Length == 1)
                        {
                            if (outType.IsGenericType && outType.GetGenericTypeDefinition() == typeof(Task<>))
                            {
                                outType = outType.GetGenericArguments().Single();
                                var inType = args[0].ParameterType;
                                if (inType.IsGenericType && inType.GetGenericTypeDefinition() == typeof(IAsyncStreamReader<>))
                                {   // Task<TResponse> ClientStreamingServerMethod(IAsyncStreamReader<TRequest> stream);
                                    // we will create: (svc, req, ctx) => svc.TheMethod(req)
                                    bound = AddMethod(inType.GetGenericArguments().Single(), outType, method, MethodType.ClientStreaming,
                                        args => Expression.Call(args[0], method, args[1]));
                                }
                                else
                                {   // Task<TResponse> UnaryServerMethod(TRequest request);
                                    // we will create: (svc, req, ctx) => svc.TheMethod(req)
                                    bound = AddMethod(inType, outType, method, MethodType.Unary,
                                        args => Expression.Call(args[0], method, args[1]));
                                }
                            }
                            else
                            {
                                var inType = args[0].ParameterType;
                                if (inType.IsGenericType && inType.GetGenericTypeDefinition() == typeof(IAsyncStreamReader<>))
                                {   // TResponse ClientStreamingServerMethod(IAsyncStreamReader<TRequest> stream);
                                    // not handled: IAsyncStreamReader is inherently async
                                }
                                else
                                {   // TResponse UnaryServerMethod(TRequest request);
                                    // we will create: (svc, req, ctx) => Task.FromResult(svc.TheMethod(req))
                                    bound = AddMethod(inType, outType, method, MethodType.Unary,
                                        args => Expression.Call(typeof(Task), nameof(Task.FromResult), new[] { method.ReturnType },
                                            Expression.Call(args[0], method, args[1])));
                                }
                            }
                        }
                        else if (args.Length == 2 && args[1].ParameterType == typeof(ServerCallContext))
                        {
                            if (outType.IsGenericType && outType.GetGenericTypeDefinition() == typeof(Task<>))
                            {
                                outType = outType.GetGenericArguments().Single();
                                var inType = args[0].ParameterType;
                                if (inType.IsGenericType && inType.GetGenericTypeDefinition() == typeof(IAsyncStreamReader<>))
                                {   // Task<TResponse> ClientStreamingServerMethod(IAsyncStreamReader<TRequest> stream, ServerCallContext serverCallContext);
                                    bound = AddMethod(inType.GetGenericArguments().Single(), outType, method, MethodType.ClientStreaming);
                                }
                                else
                                {   // Task<TResponse> UnaryServerMethod(TRequest request, ServerCallContext serverCallContext);
                                    bound = AddMethod(inType, outType, method, MethodType.Unary);
                                }
                            }
                            else
                            {
                                var inType = args[0].ParameterType;
                                if (inType.IsGenericType && inType.GetGenericTypeDefinition() == typeof(IAsyncStreamReader<>))
                                {   // TResponse ClientStreamingServerMethod(IAsyncStreamReader<TRequest> stream, ServerCallContext serverCallContext);
                                    // not handled: IAsyncStreamReader is inherently async
                                }
                                else
                                {   // TResponse UnaryServerMethod(TRequest request, ServerCallContext serverCallContext);
                                    // we will create: (svc, req, ctx) => Task.FromResult(svc.TheMethod(req, ctx))
                                    bound = AddMethod(inType, outType, method, MethodType.Unary,
                                        args => Expression.Call(typeof(Task), nameof(Task.FromResult), new[] { method.ReturnType },
                                            Expression.Call(args[0], method, args[1], args[2])));
                                }
                            }
                        }
                        else if (args.Length == 3 && args[2].ParameterType == typeof(ServerCallContext) && outType == typeof(Task)
                            && args[1].ParameterType.IsGenericType
                            && args[1].ParameterType.GetGenericTypeDefinition() == typeof(IServerStreamWriter<>))
                        {
                            outType = args[1].ParameterType.GetGenericArguments().Single();
                            var inType = args[0].ParameterType;
                            if (inType.IsGenericType && inType.GetGenericTypeDefinition() == typeof(IAsyncStreamReader<>))
                            {   // Task DuplexStreamingServerMethod(IAsyncStreamReader<TRequest> input, IServerStreamWriter<TResponse> output, ServerCallContext serverCallContext);
                                bound = AddMethod(inType.GetGenericArguments().Single(), outType, method, MethodType.DuplexStreaming);
                            }
                            else
                            {   // Task ServerStreamingServerMethod(TRequest request, IServerStreamWriter<TResponse> stream, ServerCallContext serverCallContext);
                                bound = AddMethod(inType, outType, method, MethodType.ServerStreaming);
                            }
                        }
                        if (bound)
                        {
                            _logger.Log(LogLevel.Warning, "Bound {0}.{1}", serviceContract.Name, method.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Error, "Unable to bind {0}.{1}: {2}", serviceContract.Name, method.Name, ex.Message);
                    }
                }
            }
        }
        private static T AssertNotNull<T>(T? input, [CallerMemberName] string? caller = null) where T : class
            => input ?? throw new InvalidOperationException("Invalid null from " + caller);

        private static readonly MethodInfo s_addMethod = AssertNotNull(typeof(ServicesExtensions).GetMethod(
           nameof(AddMethod), BindingFlags.Static | BindingFlags.NonPublic));

        private static void AddMethod<TService, TRequest, TResponse>(
            string serviceName, MethodInfo method, MethodType methodType,
            ServiceMethodProviderContext<TService> context, ILogger logger,
            Func<ParameterExpression[], Expression>? invoker = null)
            where TService : class
            where TRequest : class
            where TResponse : class
        {
            var oca = (OperationContractAttribute?)Attribute.GetCustomAttribute(method, typeof(OperationContractAttribute), inherit: true);
            var operationName = oca?.Name;
            if (string.IsNullOrWhiteSpace(operationName))
            {
                operationName = method.Name;
                if (operationName.EndsWith("Async")) operationName = operationName.Substring(0, operationName.Length - 5);
            }

            logger.Log(LogLevel.Warning, "Registering {0}.{1} as {2}: {3} - {4} => {5}", typeof(TService).Name, method.Name, operationName, methodType, typeof(TRequest).Name, typeof(TResponse).Name);

            var metadata = new List<object>();
            // Add type metadata first so it has a lower priority
            metadata.AddRange(typeof(TService).GetCustomAttributes(inherit: true));
            // Add method metadata last so it has a higher priority
            metadata.AddRange(method.GetCustomAttributes(inherit: true));

            TDelegate As<TDelegate>() where TDelegate : Delegate
            {
                // basic - direct call
                if (invoker == null) return (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), null, method);

                var methodParameters = typeof(TDelegate).GetMethod("Invoke")?.GetParameters() ?? Array.Empty<ParameterInfo>();
                var lambdaParameters = Array.ConvertAll(methodParameters, p => Expression.Parameter(p.ParameterType, p.Name));
                return Expression.Lambda<TDelegate>(invoker(lambdaParameters), lambdaParameters).Compile();

            }

#pragma warning disable CS8625
            switch (methodType)
            {
                case MethodType.Unary:
                    context.AddUnaryMethod(
                        new FullyNamedMethod<TRequest, TResponse>(operationName, methodType, serviceName, method.Name), metadata, As<UnaryServerMethod<TService, TRequest, TResponse>>());
                    break;
                case MethodType.ClientStreaming:
                    context.AddClientStreamingMethod(
                        new FullyNamedMethod<TRequest, TResponse>(operationName, methodType, serviceName, method.Name), metadata, As<ClientStreamingServerMethod<TService, TRequest, TResponse>>());
                    break;
                case MethodType.ServerStreaming:
                    context.AddServerStreamingMethod(
                        new FullyNamedMethod<TRequest, TResponse>(operationName, methodType, serviceName, method.Name), metadata, As<ServerStreamingServerMethod<TService, TRequest, TResponse>>());
                    break;
                case MethodType.DuplexStreaming:
                    context.AddDuplexStreamingMethod(
                        new FullyNamedMethod<TRequest, TResponse>(operationName, methodType, serviceName, method.Name), metadata, As<DuplexStreamingServerMethod<TService, TRequest, TResponse>>());
                    break;
                default:
                    throw new NotSupportedException(methodType.ToString());
            }
#pragma warning restore CS8625
        }
    }
}