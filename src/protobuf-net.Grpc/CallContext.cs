using Grpc.Core;
using System;
using System.Threading;

namespace ProtoBuf.Grpc
{
    /// <summary>
    /// Unifies the API for client and server gRPC call contexts; the API intersection is available
    /// directly - for client-specific or server-specific options: use .Client or .Server; note that
    /// whether this is a client or server context depends on the usage. Silent conversions are available.
    /// </summary>
    public readonly struct CallContext
    {
        public CallOptions Client { get; }
        public ServerCallContext? Server { get; }

        public Metadata RequestHeaders => Server == null ? Client.Headers : Server.RequestHeaders;
        public CancellationToken CancellationToken => Server == null ? Client.CancellationToken : Server.CancellationToken;
        public DateTime? Deadline => Server?.Deadline ?? Client.Deadline;
        public WriteOptions WriteOptions => Server?.WriteOptions ?? Client.WriteOptions;

        public static implicit operator CallContext(in CallOptions client) => new CallContext(client);
        public static implicit operator CallContext(ServerCallContext server) => new CallContext(server);
        public static implicit operator CallOptions(in CallContext context) => context.Client;
        public static implicit operator ServerCallContext?(in CallContext context) => context.Server;

        public CallContext(ServerCallContext server)
        {
            Client = default;
            Server = server;
        }
        public CallContext(in CallOptions client)
        {
            Client = client;
            Server = default;
        }
    }
}