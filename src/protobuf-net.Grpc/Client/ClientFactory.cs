using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Reflection;
using System.Reflection.Emit;
using Grpc.Net.Client;
using System.ServiceModel;
using ProtoBuf.Grpc.Internal;
using System.Collections.Generic;

namespace ProtoBuf.Grpc.Client
{
    //public readonly struct ClientProxy<T> : IDisposable
    //    where T : class
    //{
    //    private readonly ClientBase _client;

    //    internal ClientProxy(ClientBase client) => _client = client;


    //    public T Channel
    //    {
    //        // assume default behaviour is for the client to implement it directly, but allow alternatives
    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //        get => (T)(object)_client;
    //    }

    //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //    public void Dispose() => (_client as IDisposable)?.Dispose();

    //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //    public static implicit operator T (ClientProxy<T> proxy) => proxy.Channel;
    //}

    public static class ClientFactory
    {
        public static TService Create<TService>(HttpClient httpClient, ILoggerFactory? loggerFactory = null)
            where TService : class
            => ProxyCache<TService>.Create(httpClient, loggerFactory);

        public static TService CreateService<TClient, TService>(HttpClient httpClient, ILoggerFactory? loggerFactory = null)
                where TService : class
                where TClient : ClientBase, TService
            => GrpcClient.Create<TClient>(httpClient, loggerFactory);

        // this **abstract** inheritance is just to get access to ClientBaseConfiguration
        // (without that, this could be a static class)
        abstract class ProxyEmitter : ClientBase
        {
            private ProxyEmitter() { }

            private static readonly string ProxyIdentity = typeof(ClientFactory).Namespace + ".Proxies";

            private static readonly ModuleBuilder s_module = AssemblyBuilder.DefineDynamicAssembly(
                    new AssemblyName(ProxyIdentity), AssemblyBuilderAccess.Run).DefineDynamicModule(ProxyIdentity);

            private static readonly MethodInfo s_callInvoker = typeof(ClientBase).GetProperty(nameof(CallInvoker),
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetGetMethod(true)!;

            private static void Ldc_I4(ILGenerator il, int value)
            {
                switch(value)
                {
                    case -1: il.Emit(OpCodes.Ldc_I4_M1); break;
                    case 0: il.Emit(OpCodes.Ldc_I4_0); break;
                    case 1: il.Emit(OpCodes.Ldc_I4_1); break;
                    case 2: il.Emit(OpCodes.Ldc_I4_2); break;
                    case 3: il.Emit(OpCodes.Ldc_I4_3); break;
                    case 4: il.Emit(OpCodes.Ldc_I4_4); break;
                    case 5: il.Emit(OpCodes.Ldc_I4_5); break;
                    case 6: il.Emit(OpCodes.Ldc_I4_6); break;
                    case 7: il.Emit(OpCodes.Ldc_I4_7); break;
                    case 8: il.Emit(OpCodes.Ldc_I4_8); break;
                    default: il.Emit(OpCodes.Ldc_I4, value); break;
                }
            }

            private readonly struct ContractOperation
            {
                public string Name { get; }
                public Type From { get; }
                public Type To { get; }
                public MethodInfo Method { get; }
                public Type[] ParameterTypes { get; }
                public MethodType MethodType { get; }

                public ContractOperation(string name, Type from, Type to, MethodInfo method, MethodType methodType, Type[] parameterTypes)
                {
                    Name = name;
                    From = from;
                    To = to;
                    Method = method;
                    MethodType = methodType;
                    ParameterTypes = parameterTypes;
                }

                public static bool TryGetServiceName(Type contractType, out string? serviceName, bool demandAttribute = false)
                {
                    var sca = (ServiceContractAttribute?)Attribute.GetCustomAttribute(contractType, typeof(ServiceContractAttribute), inherit: true);
                    if (demandAttribute && sca == null)
                    {
                        serviceName = null;
                        return false;
                    }
                    serviceName = sca?.Name;
                    if (string.IsNullOrWhiteSpace(serviceName)) serviceName = contractType.Name;
                    return !string.IsNullOrWhiteSpace(serviceName);
                }

                public static List<ContractOperation> FindOperations(Type contractType, bool demandAttribute = false)
                {
                    var ops = new List<ContractOperation>();
                    foreach (var method in contractType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (method.IsGenericMethodDefinition) continue; // can't work with <T> methods

                        var oca = (OperationContractAttribute?)Attribute.GetCustomAttribute(method, typeof(OperationContractAttribute), inherit: true);
                        if (demandAttribute && oca == null) continue;
                        string? opName = oca?.Name;
                        if (string.IsNullOrWhiteSpace(opName)) opName = method.Name;
                        if (string.IsNullOrWhiteSpace(opName)) continue;

                        var parameters = method.GetParameters();

                        if (parameters.Length == 0) continue; // no way of inferring anything!

                        // for testing, I've only implemented unary naked
                        Type[] types = Array.ConvertAll(parameters, x => x.ParameterType);
                        Type from = types[0], to = method.ReturnType;
                        MethodType methodType = MethodType.Unary;
                        ops.Add(new ContractOperation(opName, from, to, method, methodType, types));
                    }
                    return ops;
                }
            }
            

            internal static Func<HttpClient, ILoggerFactory?, TService> CreateFactory<TService>()
               where TService : class
            {
                // front-load reflection discovery
                if (!typeof(TService).IsInterface)
                    throw new InvalidOperationException("Type is not an interface: " + typeof(TService).FullName);
                ContractOperation.TryGetServiceName(typeof(TService), out var serviceName);
                var ops = ContractOperation.FindOperations(typeof(TService));

                lock (s_module)
                {
                    // private sealed class IFooProxy...
                    var type = s_module.DefineType(ProxyIdentity + "." + typeof(TService).Name + "Proxy",
                        TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.NotPublic | TypeAttributes.BeforeFieldInit);

                    // : ClientBase
                    Type baseType = typeof(ClientBase);
                    type.SetParent(baseType);

                    // : TService
                    type.AddInterfaceImplementation(typeof(TService));

                    // private IFooProxy() : base() { }
                    type.DefineDefaultConstructor(MethodAttributes.Private);

                    // public IFooProxy(CallInvoker callInvoker) : base(callInvoker) { }
                    WritePassThruCtor<CallInvoker>(MethodAttributes.Public);

                    // public IFooProxy(Channel channel) : base(callIchannelnvoker) { }
                    WritePassThruCtor<Channel>(MethodAttributes.Public);

                    // private IFooProxy(ClientBaseConfiguration configuration) : base(configuration) { }
                    WritePassThruCtor<ClientBaseConfiguration>(MethodAttributes.Family);

                    var cctor = type.DefineTypeInitializer().GetILGenerator();
                    
                    // add each method of the interface
                    int fieldIndex = 0;
                    foreach (var op in ops)
                    {
                        var impl = type.DefineMethod(typeof(TService).Name + "." + op.Method.Name,
                            MethodAttributes.HideBySig | MethodAttributes.Final | MethodAttributes.NewSlot | MethodAttributes.Private | MethodAttributes.Virtual,
                            op.Method.CallingConvention, op.Method.ReturnType, op.ParameterTypes);
                        

                        Type[] fromTo = new Type[] { op.From, op.To };
                        // static readonly Method<from, to> s_SayHellosAsync
                        var field = type.DefineField("s_" + fieldIndex++, typeof(Method<,>).MakeGenericType(fromTo), FieldAttributes.Static | FieldAttributes.Private | FieldAttributes.InitOnly);
                        // = new FullyNamedMethod<from, to>(opName, methodType, serviceName, method.Name);
                        cctor.Emit(OpCodes.Ldstr, op.Name);
                        Ldc_I4(cctor, (int) op.MethodType);
                        cctor.Emit(OpCodes.Ldstr, serviceName);
                        cctor.Emit(OpCodes.Ldstr, op.Method.Name);
                        cctor.Emit(OpCodes.Newobj, typeof(FullyNamedMethod<,>).MakeGenericType(fromTo));
                        cctor.Emit(OpCodes.Stsfld, field);

                        // implement the method
                        var il = impl.GetILGenerator();
                        il.Emit(OpCodes.Ldarg_0); // this.
                        il.EmitCall(OpCodes.Callvirt, s_callInvoker, null); // get_CallInvoker

                        il.Emit(OpCodes.Ldsfld, field); // method - BadImageFormatException: Bad method token; what the hell?
                        // il.Emit(OpCodes.Ldnull); // method - this is NRE, obviously - but it isn't bad image

                        il.Emit(OpCodes.Ldnull); // host: always null
                        il.Emit(OpCodes.Ldarg_2); // options
                        il.Emit(OpCodes.Ldarg_1); // request

                        // this.CallInvoker.AsyncUnaryCall<From,To>(method, host, options, request)
                        il.EmitCall(OpCodes.Callvirt, typeof(CallInvoker).GetMethod(nameof(CallInvoker.AsyncUnaryCall))!.MakeGenericMethod(fromTo), null);
                        il.Emit(OpCodes.Ret); // return

                        // mark it as the interface implementation
                        type.DefineMethodOverride(impl, op.Method);


                    }

                    cctor.Emit(OpCodes.Ret); // end the type initializer

                    // return the factory method
                    return (Func<HttpClient, ILoggerFactory?, TService>)Delegate.CreateDelegate(typeof(Func<HttpClient, ILoggerFactory?, TService>), null,
                        typeof(ClientFactory).GetMethod(nameof(CreateService), BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(new[] { type.CreateType(), typeof(TService) }));
                    
                    void WritePassThruCtor<T>(MethodAttributes accessibility)
                    {
                        var signature = new [] { typeof(T) };
                        var baseCtor = baseType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, signature, null);
                        if (baseCtor != null)
                        {
                            var ctor = type.DefineConstructor(accessibility, CallingConventions.HasThis, signature);
                            var il = ctor.GetILGenerator();
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Ldarg_1);
                            il.Emit(OpCodes.Call, baseCtor);
                            il.Emit(OpCodes.Ret);
                        }
                    }
                }

            }
        }

        internal static class ProxyCache<TService>
           where TService : class
        {
            public static TService Create(HttpClient httpClient, ILoggerFactory? loggerFactory) => s_ctor(httpClient, loggerFactory);
            private static readonly Func<HttpClient, ILoggerFactory?, TService> s_ctor = ProxyEmitter.CreateFactory<TService>();
        }
    }
}
