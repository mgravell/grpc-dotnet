using Grpc.Core;
using System;
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
        public static T CreateClient<T>(Channel channel) where T : class
            => ProxyCache<T>.Create(channel);

        static class ProxyEmitter
        {
            private const string ProxyIdentity = "ProtoBufNetGeneratedProxies";

            static ProxyEmitter()
            {
                AssemblyBuilder asm = AssemblyBuilder.DefineDynamicAssembly(
                    new AssemblyName(ProxyIdentity), AssemblyBuilderAccess.Run);
                _module = asm.DefineDynamicModule(ProxyIdentity);
            }
            static readonly ModuleBuilder _module;

            internal static Func<Channel, TService> CreateFactory<TService>()
               where TService : class
            {
                lock (_module)
                {
                    // private sealed clas FooProxy : ClientBase
                    var type = _module.DefineType(ProxyIdentity + "." + typeof(TService).Name + "Proxy",
                        TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.NotPublic,
                        parent: typeof(ClientBase));

                    // : TService
                    type.AddInterfaceImplementation(typeof(TService));

                    // public FooProxy(Channel channel)
                    var ctor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, s_ctorSignature);
                    var il = ctor.GetILGenerator();

                    // => base(channel) {}
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, s_baseCtor);
                    il.Emit(OpCodes.Ret);

                }

                throw new NotImplementedException();
            }
            static readonly Type[] s_ctorSignature = new Type[] { typeof(Channel) };
            static readonly ConstructorInfo s_baseCtor = typeof(ClientBase)
                .GetConstructor(s_ctorSignature);
        }

        internal static class ProxyCache<TService>
           where TService : class
        {
            public static TService Create(Channel channel) => s_ctor(channel);
            private static readonly Func<Channel, TService> s_ctor = ProxyEmitter.CreateFactory<TService>();
        }
    }
}
