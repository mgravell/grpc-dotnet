using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Reflection;
using System.Reflection.Emit;
using Grpc.Net.Client;
using System.ServiceModel;
using ProtoBuf.Grpc.Internal;

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

            internal static Func<HttpClient, ILoggerFactory?, TService> CreateFactory<TService>()
               where TService : class
            {
                lock (s_module)
                {
                    var sca = (ServiceContractAttribute?)Attribute.GetCustomAttribute(typeof(TService), typeof(ServiceContractAttribute), inherit: true);
                    string? serviceName = sca?.Name;
                    if (string.IsNullOrWhiteSpace(serviceName)) serviceName = typeof(TService).Name;

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
                    foreach (var method in typeof(TService).GetMethods())
                    {
                        var oca = (OperationContractAttribute?)Attribute.GetCustomAttribute(method, typeof(OperationContractAttribute), inherit: true);
                        string? opName = oca?.Name;
                        if (string.IsNullOrWhiteSpace(opName)) opName = method.Name;
                        if (string.IsNullOrWhiteSpace(opName)) continue;

                        // for testing, I've only implemented unary naked

                        var parameters = method.GetParameters();
                        var impl = type.DefineMethod(typeof(TService).Name + "." + method.Name,
                            MethodAttributes.HideBySig | MethodAttributes.Final | MethodAttributes.NewSlot | MethodAttributes.Private | MethodAttributes.Virtual,
                            method.CallingConvention, method.ReturnType, Array.ConvertAll(parameters, p => p.ParameterType));
                        

                        Type from = parameters[0].ParameterType, to = method.ReturnType;
                        MethodType methodType = MethodType.Unary;


                        Type[] fromTo = new Type[] { from, to };
                        // static readonly Method<from, to> s_SayHellosAsync
                        var field = type.DefineField("s_" + fieldIndex++, typeof(Method<,>).MakeGenericType(fromTo), FieldAttributes.Static | FieldAttributes.Private | FieldAttributes.InitOnly);
                        // = new FullyNamedMethod<from, to>(opName, methodType, serviceName, method.Name);
                        cctor.Emit(OpCodes.Ldstr, opName);
                        Ldc_I4(cctor, (int) methodType);
                        cctor.Emit(OpCodes.Ldstr, serviceName);
                        cctor.Emit(OpCodes.Ldstr, method.Name);
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
                        type.DefineMethodOverride(impl, method);


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
