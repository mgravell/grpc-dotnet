using Grpc.Core;
using System;
using System.Threading;

namespace protobuf_net.Grpc
{
    public readonly struct CallContext
    {
        public CallOptions Client { get; }
        public ServerCallContext? Server { get; }

        public Metadata RequestHeaders => Server == null ? Client.Headers : Server.RequestHeaders;
        public CancellationToken CancellationToken => Server == null ? Client.CancellationToken : Server.CancellationToken;
        public DateTime? Deadline => Server?.Deadline ?? Client.Deadline;

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