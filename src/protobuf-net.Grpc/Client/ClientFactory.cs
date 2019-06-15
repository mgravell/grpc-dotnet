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
using System.Linq;
using System.Threading.Tasks;

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

            private static readonly MethodInfo
                s_callInvoker = typeof(ClientBase).GetProperty(nameof(CallInvoker),
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetGetMethod(true)!,
                s_callContext_Client = typeof(CallContext).GetProperty(nameof(CallContext.Client),
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetGetMethod(true)!,
                s_callContext_Prepare = typeof(CallContext).GetMethod(nameof(CallContext.Prepare), BindingFlags.Public | BindingFlags.Instance)!,

#pragma warning disable CS0618
                s_reshapeTaskT = typeof(Reshape).GetMethod(nameof(Reshape.AsTask), BindingFlags.Public | BindingFlags.Static)!,
                s_reshapeValueTaskT = typeof(Reshape).GetMethod(nameof(Reshape.AsValueTask), BindingFlags.Public | BindingFlags.Static)!,
                s_reshapeSyncT = typeof(Reshape).GetMethod(nameof(Reshape.AsSync), BindingFlags.Public | BindingFlags.Static)!;
#pragma warning restore CS0618

            private static void Ldc_I4(ILGenerator il, int value)
            {
                switch (value)
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
                    case int i when (i >= -128 & i < 127): il.Emit(OpCodes.Ldc_I4_S, (sbyte)i); break;
                    default: il.Emit(OpCodes.Ldc_I4, value); break;
                }
            }

            private static void LoadDefault<T>(ILGenerator il) where T : struct
            {
                var local = il.DeclareLocal(typeof(T));
                Ldloca(il, local);
                il.Emit(OpCodes.Initobj, typeof(T));
                Ldloc(il, local);
            }

            private static void Ldloc(ILGenerator il, LocalBuilder local)
            {
                switch(local.LocalIndex)
                {
                    case 0: il.Emit(OpCodes.Ldloc_0); break;
                    case 1: il.Emit(OpCodes.Ldloc_1); break;
                    case 2: il.Emit(OpCodes.Ldloc_2); break;
                    case 3: il.Emit(OpCodes.Ldloc_3); break;
                    case int i when (i >= 0 & i <= 255): il.Emit(OpCodes.Ldloc_S, (byte)i); break;
                    default: il.Emit(OpCodes.Ldloc, local); break;
                }
            }

            private static void Ldloca(ILGenerator il, LocalBuilder local)
            {
                switch(local.LocalIndex)
                {
                    case int i when (i >= 0 & i <= 255): il.Emit(OpCodes.Ldloca_S, (byte)i); break;
                    default: il.Emit(OpCodes.Ldloca, local); break;
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
                public ContextKind Context { get; }

                public ContractOperation(string name, Type from, Type to, MethodInfo method, MethodType methodType, ContextKind contextKind, Type[] parameterTypes)
                {
                    Name = name;
                    From = from;
                    To = to;
                    Method = method;
                    MethodType = methodType;
                    Context = contextKind;
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
                        if (string.IsNullOrWhiteSpace(opName))
                        {
                            opName = method.Name;
                            if (opName.EndsWith("Async"))
                                opName = opName.Substring(0, opName.Length - 5);
                        }
                        if (string.IsNullOrWhiteSpace(opName)) continue;

                        var parameters = method.GetParameters();

                        if (parameters.Length == 0 || parameters.Length > 2) continue; // no way of inferring anything!

                        ContextKind? contextKind = null;
                        if (parameters.Length == 1)
                        {
                            contextKind = ContextKind.None;
                        }
                        else if (parameters[1].ParameterType == typeof(CallOptions))
                        {
                            contextKind = ContextKind.CallOptions;
                        }
                        else if (parameters[1].ParameterType == typeof(CallContext))
                        {
                            contextKind = ContextKind.CallContext;
                        }
                        if (contextKind == null) continue; // unknown context type

                        // for testing, I've only implemented unary naked
                        Type[] types = Array.ConvertAll(parameters, x => x.ParameterType);
                        Type from = types[0], to = method.ReturnType;
                        if (to.IsGenericType)
                        {
                            var genType = to.GetGenericTypeDefinition();
                            if (genType == typeof(AsyncUnaryCall<>) || genType == typeof(Task<>) || genType == typeof(ValueTask<>))
                            {
                                to = to.GetGenericArguments()[0];
                            }
                        }
                        MethodType methodType = MethodType.Unary;

                        ops.Add(new ContractOperation(opName, from, to, method, methodType, contextKind.Value, types));
                    }
                    return ops;
                }

                internal bool IsSyncT()
                {
                    return Method.ReturnType == To;
                }
                internal bool IsTaskT()
                {
                    var ret = Method.ReturnType;
                    return ret.IsGenericType && ret.GetGenericTypeDefinition() == typeof(Task<>)
                        && ret.GetGenericArguments()[0] == To;
                }
                internal bool IsValueTaskT()
                {
                    var ret = Method.ReturnType;
                    return ret.IsGenericType && ret.GetGenericTypeDefinition() == typeof(ValueTask<>)
                        && ret.GetGenericArguments()[0] == To;
                }
            }

            internal enum ContextKind
            {
                None, // no context
                CallOptions, // GRPC core client context kind
                CallContext, // pb-net shared context kind
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
                    var type = s_module.DefineType(ProxyIdentity + "." + typeof(TService).Name + "_Proxy",
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
                        Type[] fromTo = new Type[] { op.From, op.To };
                        // public static readonly Method<from, to> s_{i}
                        var field = type.DefineField("s_op_" + fieldIndex++, typeof(Method<,>).MakeGenericType(fromTo),
                            FieldAttributes.Static | FieldAttributes.Public | FieldAttributes.InitOnly);
                        // = new FullyNamedMethod<from, to>(opName, methodType, serviceName, method.Name);
                        cctor.Emit(OpCodes.Ldstr, op.Name); // opName
                        Ldc_I4(cctor, (int)op.MethodType); // methodType
                        cctor.Emit(OpCodes.Ldstr, serviceName); // serviceName
                        cctor.Emit(OpCodes.Ldnull); // methodName: leave null (uses opName)
                        cctor.Emit(OpCodes.Ldnull); // requestMarshaller: always null
                        cctor.Emit(OpCodes.Ldnull); // responseMarshaller: always null
                        cctor.Emit(OpCodes.Newobj, typeof(FullyNamedMethod<,>).MakeGenericType(fromTo)
                            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Single()); // new FullyNamedMethod
                        cctor.Emit(OpCodes.Stsfld, field);

                        var impl = type.DefineMethod(typeof(TService).Name + "." + op.Method.Name,
                            MethodAttributes.HideBySig | MethodAttributes.Final | MethodAttributes.NewSlot | MethodAttributes.Private | MethodAttributes.Virtual,
                            op.Method.CallingConvention, op.Method.ReturnType, op.ParameterTypes);

                        // implement the method
                        var il = impl.GetILGenerator();
                        il.Emit(OpCodes.Ldarg_0); // this.
                        il.EmitCall(OpCodes.Callvirt, s_callInvoker, null); // get_CallInvoker
                        il.Emit(OpCodes.Ldsfld, field); // method
                        il.Emit(OpCodes.Ldnull); // host: always null
                        switch (op.Context)
                        {
                            case ContextKind.None:
                                LoadDefault<CallOptions>(il);
                                break;
                            case ContextKind.CallOptions:
                                il.Emit(OpCodes.Ldarg_2); // options
                                break;
                            case ContextKind.CallContext:
                                il.Emit(OpCodes.Ldarga_S, (byte)2);
                                il.EmitCall(OpCodes.Call, s_callContext_Client, null);
                                break;
                            default:
                                throw new NotSupportedException("Unsupported call-context kind: " + op.Context);
                        }

                        il.Emit(OpCodes.Ldarg_1); // request

                        // this.CallInvoker.AsyncUnaryCall<From,To>(method, host, options, request)
                        il.EmitCall(OpCodes.Callvirt, typeof(CallInvoker).GetMethod(nameof(CallInvoker.AsyncUnaryCall))!.MakeGenericMethod(fromTo), null);

                        if (op.IsSyncT())
                        {
                            EmitStandardReshape(s_reshapeSyncT);
                        }
                        else if (op.IsTaskT())
                        {
                            EmitStandardReshape(s_reshapeTaskT);
                        }
                        else if (op.IsValueTaskT())
                        {
                            EmitStandardReshape(s_reshapeValueTaskT);
                        }
                        void EmitStandardReshape(MethodInfo reshaper)
                        {
                            switch(op.Context)
                            {
                                case ContextKind.CallContext:
                                    il.Emit(OpCodes.Ldarga_S, (byte)2);
                                    il.EmitCall(OpCodes.Call, s_callContext_Prepare, null);
                                    break;
                                default:
                                    il.Emit(OpCodes.Ldnull); // no metadata capture
                                    break;
                            }
                            il.EmitCall(OpCodes.Call, reshaper.MakeGenericMethod(op.To), null);
                        }

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
                        var signature = new[] { typeof(T) };
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
