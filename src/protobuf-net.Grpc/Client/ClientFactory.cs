using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Internal;
using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Reflection.Emit;

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

            private static readonly MethodInfo s_ClientBase_CallInvoker = typeof(ClientBase).GetProperty(nameof(CallInvoker),
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetGetMethod(true)!,
                s_Object_ToString = typeof(object).GetMethod(nameof(object.ToString))!;
            private static readonly FieldInfo s_CallContext_Default = typeof(CallContext).GetField(nameof(CallContext.Default))!;

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
                switch (local.LocalIndex)
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
                switch (local.LocalIndex)
                {
                    case int i when (i >= 0 & i <= 255): il.Emit(OpCodes.Ldloca_S, (byte)i); break;
                    default: il.Emit(OpCodes.Ldloca, local); break;
                }
            }
            private static void Ldarga(ILGenerator il, ushort index)
            {
                if (index <= 255)
                {
                    il.Emit(OpCodes.Ldarga_S, (byte)index);
                }
                else
                {
                    il.Emit(OpCodes.Ldarga, index);
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
                    {
                        var toString = type.DefineMethod(nameof(ToString), s_Object_ToString.Attributes, s_Object_ToString.CallingConvention,
                        typeof(string), Type.EmptyTypes);
                        var il = toString.GetILGenerator();
                        il.Emit(OpCodes.Ldstr, serviceName);
                        il.Emit(OpCodes.Ret);
                        type.DefineMethodOverride(toString, s_Object_ToString);
                    }

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

                        switch(op.Context)
                        {
                            case ContextKind.CallOptions:
                                // we only support this for signatures that match the exat google pattern, but:
                                // defer for now
                                il.ThrowException(typeof(NotImplementedException));
                                break;
                            case ContextKind.NoContext:
                            case ContextKind.CallContext:
                                // typically looks something like (where this is an extension method on Reshape):
                                // => context.{ReshapeMethod}(CallInvoker, {method}, request, [host: null]);
                                var method = op.TryGetClientHelper();
                                if (method == null)
                                {
                                    // unexpected, but...
                                    il.ThrowException(typeof(NotSupportedException));
                                }
                                else
                                {
                                    if (op.Context == ContextKind.CallContext)
                                    {
                                        Ldarga(il, 2);
                                    }
                                    else
                                    {
                                        il.Emit(OpCodes.Ldsflda, s_CallContext_Default);
                                    }
                                    il.Emit(OpCodes.Ldarg_0); // this.
                                    il.EmitCall(OpCodes.Callvirt, s_ClientBase_CallInvoker, null); // get_CallInvoker

                                    il.Emit(OpCodes.Ldsfld, field); // {method}
                                    il.Emit(OpCodes.Ldarg_1); // request
                                    il.Emit(OpCodes.Ldnull); // host (always null)
                                    il.EmitCall(OpCodes.Call, method, null);
                                    il.Emit(OpCodes.Ret); // return
                                }
                                break;
                        case ContextKind.ServerCallContext: // server call? we're writing a client!
                            default: // who knows!
                                il.ThrowException(typeof(NotSupportedException));
                                break;
                        }

                        // mark it as the interface implementation
                        type.DefineMethodOverride(impl, op.Method);
                    }

                    cctor.Emit(OpCodes.Ret); // end the type initializer (after creating all the field types)

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
