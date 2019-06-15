using System;
using System.Runtime.CompilerServices;
using Grpc.Core;

namespace ProtoBuf.Grpc.Internal
{
    public sealed class MetadataContext
    {
        internal MetadataContext() { }

        private Metadata? _headers, _trailers;
        internal Metadata Headers
        {
            get => _headers ?? Throw("Headers are not yet available");
            set => _headers = value;
        }
        internal Metadata Trailers
        {
            get => _trailers ?? Throw("Trailers are not yet available");
            set => _trailers = value;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Metadata Throw(string message) => throw new InvalidOperationException(message);

        internal MetadataContext Reset()
        {
            _headers = _trailers = null;
            return this;
        }
    }
}
