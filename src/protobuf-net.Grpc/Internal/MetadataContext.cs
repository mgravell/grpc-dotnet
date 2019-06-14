using System;
using System.Runtime.CompilerServices;
using Grpc.Core;

namespace ProtoBuf.Grpc.Internal
{
    public sealed class MetadataContext
    {
        internal MetadataContext() { }

        private Metadata? _headers, _trailers;
        public Metadata Headers => _headers ?? Throw("Headers are not yet available");
        public Metadata Trailers => _trailers ?? Throw("Trailers are not yet available");

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Metadata Throw(string message) => throw new InvalidOperationException(message);

        internal void SetHeaders(Metadata metadata) => _headers = metadata;

        internal void SetTrailers(Metadata metadata) => _trailers = metadata;
    }
}
