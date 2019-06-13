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
using System.Buffers;

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

            // do **not** replace these with a `params` etc version; the point here is to be as cheap
            // as possible for misses
            private static bool IsMatch(Type returnType, ParameterInfo[] parameters, Type?[] types, Type? tRet)
                => parameters.Length == 0
                && IsMatch(tRet, returnType, out types[0]);
            private static bool IsMatch(Type returnType, ParameterInfo[] parameters, Type?[] types, Type? t0, Type? tRet)
                => parameters.Length == 1
                && IsMatch(t0, parameters[0].ParameterType, out types[0])
                && IsMatch(tRet, returnType, out types[1]);
            private static bool IsMatch(Type returnType, ParameterInfo[] parameters, Type?[] types, Type? t0, Type? t1, Type? tRet)
                => parameters.Length == 2
                && IsMatch(t0, parameters[0].ParameterType, out types[0])
                && IsMatch(t1, parameters[1].ParameterType, out types[1])
                && IsMatch(tRet, returnType, out types[2]);
            private static bool IsMatch(Type returnType, ParameterInfo[] parameters, Type?[] types, Type? t0, Type? t1, Type? t2, Type? tRet)
                => parameters.Length == 3
                && IsMatch(t0, parameters[0].ParameterType, out types[0])
                && IsMatch(t1, parameters[1].ParameterType, out types[1])
                && IsMatch(t2, parameters[2].ParameterType, out types[2])
                && IsMatch(tRet, returnType, out types[3]);

            private static bool IsMatch(in Type? template, in Type actual, out Type result)
            {
                if (template == null || template == actual)
                {
                    result = actual;
                    return true;
                } // fine
                if (actual.IsGenericType && template.IsGenericTypeDefinition
                    && actual.GetGenericTypeDefinition() == template)
                {
                    // expected Foo<>, got Foo<T>: report T
                    result = actual.GetGenericArguments()[0];
                    return true;
                }
                result = typeof(void);
                return false;
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
                    if (method.IsGenericMethodDefinition) continue; // no generics - what T would we use?

                    Type[] types = ArrayPool<Type>.Shared.Rent(8);
                    try
                    {
                        if (isPublicContract && !Attribute.IsDefined(method, typeof(OperationContractAttribute)))
                            continue; // for methods on the class (not a service contract interface): demand [OperationContract]

                        bool bound = false;
                        var outType = method.ReturnType;
                        var args = method.GetParameters();

                        // IsMatch takes a signature which may contain wildcards for parameters
                        // and the return type; it validates the signature, and fills in the actual
                        // types (resolving generic types down to their T etc) into the buffer "types"
                        if (IsMatch(outType, args, types, typeof(IAsyncStreamReader<>), typeof(ServerCallContext), null)) // Foo(reader, ctx) => *
                        {
                            bound = AddMethod(types[0], types[1], method, MethodType.ClientStreaming);
                        }
                        else if (IsMatch(outType, args, types, typeof(IAsyncStreamReader<>), null)) // Foo(reader) => *
                        {
                            bound = AddMethod(types[0], types[1], method, MethodType.ClientStreaming,
                                args => Expression.Call(args[0], method, args[1]));
                        }
                        else if (IsMatch(outType, args, types, null, typeof(ServerCallContext), null)) // Foo(*, ctx) => *
                        {
                            bound = AddMethod(types[0], types[1], method, MethodType.Unary);
                        }
                        else if (IsMatch(outType, args, types, null, null)) // Foo(*) => *
                        {
                            bound = AddMethod(types[0], types[1], method, MethodType.Unary,
                                args => Expression.Call(args[0], method, args[1]));
                        }
                        else if (IsMatch(outType, args, types, typeof(IAsyncStreamReader<>), typeof(IServerStreamWriter<>), typeof(ServerCallContext), null)) // Foo(reader, writer, ctx) => *
                        {
                            bound = AddMethod(types[0], types[1], method, MethodType.DuplexStreaming);
                        }
                        else if (IsMatch(outType, args, types, typeof(IAsyncStreamReader<>), typeof(IServerStreamWriter<>), null)) // Foo(reader, writer) => *
                        {
                            bound = AddMethod(types[0], types[1], method, MethodType.DuplexStreaming,
                                args => Expression.Call(args[0], method, args[1], args[2]));
                        }
                        else if (IsMatch(outType, args, types, null, typeof(IServerStreamWriter<>), typeof(ServerCallContext), null)) // Foo(*, writer, ctx) => *
                        {
                            bound = AddMethod(types[0], types[1], method, MethodType.ServerStreaming);
                        }
                        else if (IsMatch(outType, args, types, null, typeof(IServerStreamWriter<>), null)) // Foo(*, writer) => *
                        {
                            bound = AddMethod(types[0], types[1], method, MethodType.ServerStreaming,
                                args => Expression.Call(args[0], method, args[1], args[2]));
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
                    finally
                    {
                        ArrayPool<Type?>.Shared.Return(types);
                    }
                }
            }
        }

        private static readonly MethodInfo s_addMethod = typeof(ServicesExtensions).GetMethod(
           nameof(AddMethod), BindingFlags.Static | BindingFlags.NonPublic)!;

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
                var finalSignature = typeof(TDelegate).GetMethod("Invoke")!;

                if (invoker == null && method.ReturnType == finalSignature.ReturnType) return (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), null, method);

                var methodParameters = finalSignature.GetParameters();
                var lambdaParameters = Array.ConvertAll(methodParameters, p => Expression.Parameter(p.ParameterType, p.Name));
                var body = invoker?.Invoke(lambdaParameters) ?? Expression.Call(lambdaParameters[0], method, lambdaParameters.Skip(1));

                if (body.Type != finalSignature.ReturnType)
                {
                    if (finalSignature.ReturnType.IsGenericType && finalSignature.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                    {
                        // gRPC expects a Task<T>, and that isn't what we are returning; fake it

                        // is it a ValueTask<T>?
                        if (body.Type.IsGenericType && body.Type.GetGenericTypeDefinition() == typeof(ValueTask<>))
                        {   // foo.AsTask()
                            body = Expression.Call(body, "AsTask", null);
                        }
                        else
                        {   // Task.FromResult(foo)
                            body = Expression.Call(typeof(Task), nameof(Task.FromResult), finalSignature.ReturnType.GetGenericArguments(), body);
                        }
                    }
                }
                var lambda = Expression.Lambda<TDelegate>(body, lambdaParameters);
                logger.Log(LogLevel.Warning, "mapped {0} via {1}", operationName, lambda);
                return lambda.Compile();
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