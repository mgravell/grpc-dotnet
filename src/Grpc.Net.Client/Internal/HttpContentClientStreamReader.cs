﻿#region Copyright notice and license

// Copyright 2019 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Grpc.Net.Client.Internal
{
    internal class HttpContentClientStreamReader<TRequest, TResponse> : IAsyncStreamReader<TResponse>
        where TRequest : class
        where TResponse : class
    {
        // Getting logger name from generic type is slow
        private const string LoggerName = "Grpc.Net.Client.Internal.HttpContentClientStreamReader";

        private static readonly Task<bool> FinishedTask = Task.FromResult(false);

        private readonly GrpcCall<TRequest, TResponse> _call;
        private readonly ILogger _logger;
        private readonly object _moveNextLock;

        public TaskCompletionSource<(HttpResponseMessage, Status?)> HttpResponseTcs { get; }

        private HttpResponseMessage? _httpResponse;
        private string? _grpcEncoding;
        private Stream? _responseStream;
        private Task<bool>? _moveNextTask;

        public HttpContentClientStreamReader(GrpcCall<TRequest, TResponse> call)
        {
            _call = call;
            _logger = call.Channel.LoggerFactory.CreateLogger(LoggerName);
            _moveNextLock = new object();

            HttpResponseTcs = new TaskCompletionSource<(HttpResponseMessage, Status?)>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        // IAsyncStreamReader<T> should declare Current as nullable
        // Suppress warning when overriding interface definition
#pragma warning disable CS8613 // Nullability of reference types in return type doesn't match implicitly implemented member.
        public TResponse? Current { get; private set; }
#pragma warning restore CS8613 // Nullability of reference types in return type doesn't match implicitly implemented member.

        public void Dispose()
        {
        }

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            _call.EnsureNotDisposed();

            // HTTP response has finished
            if (_call.CancellationToken.IsCancellationRequested)
            {
                if (!_call.Channel.ThrowOperationCanceledOnCancellation)
                {
                    return Task.FromException<bool>(_call.CreateCanceledStatusException());
                }
                else
                {
                    return Task.FromCanceled<bool>(_call.CancellationToken);
                }
            }

            if (_call.CallTask.IsCompletedSuccessfully)
            {
                var status = _call.CallTask.Result;
                if (status.StatusCode == StatusCode.OK)
                {
                    // Response is finished and it was successful so just return false
                    return FinishedTask;
                }
                else
                {
                    return Task.FromException<bool>(_call.CreateRpcException(status));
                }
            }

            lock (_moveNextLock)
            {
                using (_call.StartScope())
                {
                    // Pending move next need to be awaited first
                    if (IsMoveNextInProgressUnsynchronized)
                    {
                        var ex = new InvalidOperationException("Can't read the next message because the previous read is still in progress.");
                        Log.ReadMessageError(_logger, ex);
                        return Task.FromException<bool>(ex);
                    }

                    // Save move next task to track whether it is complete
                    _moveNextTask = MoveNextCore(cancellationToken);
                }
            }

            return _moveNextTask;
        }

        private async Task<bool> MoveNextCore(CancellationToken cancellationToken)
        {
            CancellationTokenSource? cts = null;
            try
            {
                // Linking tokens is expensive. Only create a linked token if the token passed in requires it
                if (cancellationToken.CanBeCanceled)
                {
                    cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _call.CancellationToken);
                    cancellationToken = cts.Token;
                }
                else
                {
                    cancellationToken = _call.CancellationToken;
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (_httpResponse == null)
                {
                    var (httpResponse, status) = await HttpResponseTcs.Task.ConfigureAwait(false);
                    if (status != null && status.Value.StatusCode != StatusCode.OK)
                    {
                        throw _call.CreateFailureStatusException(status.Value);
                    }

                    _httpResponse = httpResponse;
                    _grpcEncoding = GrpcProtocolHelpers.GetGrpcEncoding(_httpResponse);
                }
                if (_responseStream == null)
                {
                    try
                    {
                        _responseStream = await _httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                        // The response was disposed while waiting for the content stream to start.
                        // This will happen if there is no content stream (e.g. a streaming call finishes with no messages).
                        // Treat this like a cancellation.
                        throw new OperationCanceledException();
                    }
                }

                Debug.Assert(_grpcEncoding != null, "Encoding should have been calculated from response.");

                var tuple = await _call.ReadMessageAsync(
                    _responseStream,
                    _grpcEncoding,
                    singleMessage: false,
                    cancellationToken).ConfigureAwait(false);
                Current = tuple.Value;
                if (!tuple.HasValue)
                {
                    // No more content in response so report status to call.
                    // The call will handle finishing the response.
                    var status = GrpcProtocolHelpers.GetResponseStatus(_httpResponse);
                    _call.ResponseStreamEnded(status);
                    if (status.StatusCode != StatusCode.OK)
                    {
                        throw _call.CreateFailureStatusException(status);
                    }

                    return false;
                }

                GrpcEventSource.Log.MessageReceived();
                return true;
            }
            catch (OperationCanceledException) when (!_call.Channel.ThrowOperationCanceledOnCancellation)
            {
                if (_call.ResponseFinished)
                {
                    // Call status will have been set before dispose.
                    var status = await _call.CallTask.ConfigureAwait(false);
                    if (status.StatusCode == StatusCode.OK)
                    {
                        // Return false to indicate that the stream is complete without a message.
                        return false;
                    }
                }

                throw _call.CreateCanceledStatusException();
            }
            finally
            {
                cts?.Dispose();
            }
        }

        /// <summary>
        /// A value indicating whether there is an async move next already in progress.
        /// Should only check this property when holding the move next lock.
        /// </summary>
        private bool IsMoveNextInProgressUnsynchronized
        {
            get
            {
                var moveNextTask = _moveNextTask;
                return moveNextTask != null && !moveNextTask.IsCompleted;
            }
        }

        private static class Log
        {
            private static readonly Action<ILogger, Exception> _readMessageError =
                LoggerMessage.Define(LogLevel.Error, new EventId(1, "ReadMessageError"), "Error reading message.");

            public static void ReadMessageError(ILogger logger, Exception ex)
            {
                _readMessageError(logger, ex);
            }
        }
    }
}
