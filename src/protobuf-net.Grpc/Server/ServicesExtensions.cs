using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
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

                _logger.Log(LogLevel.Warning, "pb-net processing {0}", typeof(TService).Name);

                var svcType = typeof(TService);
                var sva = (ServiceContractAttribute?)Attribute.GetCustomAttribute(svcType, typeof(ServiceContractAttribute));
                var serviceName = sva?.Name;
                if (string.IsNullOrWhiteSpace(serviceName)) serviceName = svcType.FullName?.Replace('+', '.');
                if (string.IsNullOrWhiteSpace(serviceName)) return; // no shirt, no shoes, no service

                #region Utility method to invoke AddMethod<,,>
                object?[]? argsBuffer = null;
                Type[] typesBuffer = Array.Empty<Type>();
                void AddMethod(Type @in, Type @out, MethodInfo m, MethodType t)
                {
                    if (typesBuffer.Length == 0)
                    {
                        typesBuffer = new Type[] { typeof(TService), typeof(void), typeof(void) };
                    }
                    typesBuffer[1] = @in;
                    typesBuffer[2] = @out;

                    if (argsBuffer == null)
                    {
                        argsBuffer = new object?[] { serviceName, null, null, context, _logger };
                    }
                    argsBuffer[1] = m;
                    argsBuffer[2] = t;

                    s_addMethod.MakeGenericMethod(typesBuffer).Invoke(null, argsBuffer);
                }
                #endregion

                foreach (var method in svcType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    try
                    {
                        var outType = method.ReturnType;
                        if (outType == null) continue;

                        var args = method.GetParameters();
                        if (args.Length == 2 && args[1].ParameterType == typeof(ServerCallContext)
                            && outType.IsGenericType && outType.GetGenericTypeDefinition() == typeof(Task<>))
                        {
                            outType = outType.GetGenericArguments().Single();
                            var inType = args[0].ParameterType;
                            if (inType.IsGenericType && inType.GetGenericTypeDefinition() == typeof(IAsyncStreamReader<>))
                            {   // Task<TResponse> ClientStreamingServerMethod(IAsyncStreamReader<TRequest> stream, ServerCallContext serverCallContext);
                                AddMethod(inType.GetGenericArguments().Single(), outType, method, MethodType.ClientStreaming);

                            }
                            else
                            {   // Task<TResponse> UnaryServerMethod(TRequest request, ServerCallContext serverCallContext);
                                AddMethod(inType, outType, method, MethodType.Unary);
                            }
                        }
                        else if (args.Length == 3 && args[2].ParameterType == typeof(ServerCallContext) && outType == typeof(Task)
                            && args[1].ParameterType.IsGenericType
                            && args[1].ParameterType.GetGenericTypeDefinition() == typeof(IServerStreamWriter<>))
                        {
                            outType = args[1].ParameterType.GetGenericArguments().Single();
                            var inType = args[0].ParameterType;
                            if (inType.IsGenericType && inType.GetGenericTypeDefinition() == typeof(IAsyncStreamReader<>))
                            {
                                // Task DuplexStreamingServerMethod(IAsyncStreamReader<TRequest> input, IServerStreamWriter<TResponse> output, ServerCallContext serverCallContext);
                                AddMethod(inType.GetGenericArguments().Single(), outType, method, MethodType.DuplexStreaming);
                            }
                            else
                            {
                                // Task ServerStreamingServerMethod(TRequest request, IServerStreamWriter<TResponse> stream, ServerCallContext serverCallContext);
                                AddMethod(inType, outType, method, MethodType.ServerStreaming);
                            }
                        }
                        else
                        {
                            continue; // not a pattern we recognize
                        }
                        _logger.Log(LogLevel.Warning, "Bound {0}.{1}", svcType.Name, method.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Error, "Unable to bind {0}.{1}: {2}", svcType.Name, method.Name, ex.Message);
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
            ServiceMethodProviderContext<TService> context, ILogger logger)
            where TService : class
            where TRequest : class
            where TResponse : class
        {
            var oca = (OperationContractAttribute?)Attribute.GetCustomAttribute(method, typeof(OperationContractAttribute));
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

            T As<T>() where T : Delegate
            {
                return (T)Delegate.CreateDelegate(typeof(T), null, method);
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

        //private static void DoTheMagic<TService>(ServiceBinderBase binder, TService service)
        //{
        //    var svcType = typeof(TService);

        //    


        //    foreach (var method in svcType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        //    {
        //        var outType = method.ReturnType;
        //        if (outType == null) continue;
        //        var args = method.GetParameters();
        //        if (args.Length == 2 && args[1].ParameterType == typeof(ServerCallContext)
        //            && outType.IsGenericType && outType.GetGenericTypeDefinition() == typeof(Task<>))
        //        {
        //            outType = outType.GetGenericArguments().Single();
        //            var inType = args[0].ParameterType;
        //            if (inType.IsGenericType && inType.GetGenericTypeDefinition() == typeof(IAsyncStreamReader<>))
        //            {   // Task<TResponse> ClientStreamingServerMethod(IAsyncStreamReader<TRequest> stream, ServerCallContext serverCallContext);
        //                inType = inType.GetGenericArguments().Single();
        //                AddMethod(inType, outType, method, MethodType.ClientStreaming);

        //            }
        //            else
        //            {   // Task<TResponse> UnaryServerMethod(TRequest request, ServerCallContext serverCallContext);
        //                AddMethod(inType, outType, method, MethodType.Unary);
        //            }
        //        }
        //        else if (args.Length == 3 && args[2].ParameterType == typeof(ServerCallContext) && outType == typeof(Task)
        //            && args[1].ParameterType.IsGenericType
        //            && args[1].ParameterType.GetGenericTypeDefinition() == typeof(IServerStreamWriter<>))
        //        {
        //            outType = args[1].ParameterType.GetGenericArguments().Single();
        //            var inType = args[0].ParameterType;
        //            if (inType.IsGenericType && inType.GetGenericTypeDefinition() == typeof(IAsyncStreamReader<>))

        //            {
        //                inType = inType.GetGenericArguments().Single();
        //                // Task DuplexStreamingServerMethod(IAsyncStreamReader<TRequest> input, IServerStreamWriter<TResponse> output, ServerCallContext serverCallContext);
        //                AddMethod(inType, outType, method, MethodType.DuplexStreaming);
        //            }
        //            else
        //            {
        //                // Task ServerStreamingServerMethod(TRequest request, IServerStreamWriter<TResponse> stream, ServerCallContext serverCallContext);
        //                AddMethod(inType, outType, method, MethodType.ServerStreaming);
        //            }
        //        }
        //    }
        //}
    }
}