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
using System.Threading;
using System.Threading.Tasks;
using Common;
using Grpc.Core;
using Grpc.Net.Client;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Client;

namespace Sample.Clients
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var httpClient = ClientResources.CreateHttpClient("localhost:50051");
            //var client = GrpcClient.Create<Greet.Greeter.GreeterClient>(httpClient);
            // var client = ClientFactory.CreateService<SharedContract.ManualGreeterClient, SharedContract.IGreeter>(httpClient);
            var client = ClientFactory.Create<SharedContract.IGreeter>(httpClient);
            Console.WriteLine($"client: {client.GetType().FullName}");
            Console.WriteLine("Connecting...");

            await UnaryCallExample(client);

            // await ServerStreamingCallExample(client);

            Console.WriteLine("Shutting down");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static async Task UnaryCallExample(Greet.Greeter.GreeterClient client)
        {
            var reply = await client.SayHelloAsync(new Greet.HelloRequest { Name = "GreeterClient" });
            Console.WriteLine("Greeting: " + reply.Message);
        }
        private static async Task UnaryCallExample(SharedContract.IGreeter client)
        {
            var callContext = new CallContext(default, CallContextFlags.CaptureMetadata);
            var reply = await client.SayHelloAsync(new SharedContract.HelloRequest { Name = "GreeterClient" }, callContext);
            Console.WriteLine("Greeting: " + reply.Message);
            var status = callContext.ResponseStatus();
            Console.WriteLine($"Status: {status.StatusCode} ({status.Detail})");
            var metadata = callContext.ResponseHeaders();
            Console.WriteLine(metadata.Count);
            foreach(var header in metadata)
            {
                Console.WriteLine($"H: {header.Key}={header.Value}");
            }
            metadata = callContext.ResponseTrailers();
            foreach (var header in metadata)
            {
                Console.WriteLine($"T: {header.Key}={header.Value}");
            }
        }

        private static async Task ServerStreamingCallExample(Greet.Greeter.GreeterClient client)
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(3.5));

            using (var replies = client.SayHellos(new Greet.HelloRequest { Name = "GreeterClient" }, cancellationToken: cts.Token))
            {
                try
                {
                    while (await replies.ResponseStream.MoveNext(cts.Token))
                    {
                        Console.WriteLine("Greeting: " + replies.ResponseStream.Current.Message);
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                {
                    Console.WriteLine("Stream cancelled.");
                }
            }
        }

        //private static async Task ServerStreamingCallExample(SharedContract.IGreeter client)
        //{
        //    var replies = client.SayHellos(new SharedContract.HelloRequest { Name = "GreeterClient" });
        //    while (await replies.ResponseStream.MoveNext(CancellationToken.None))
        //    {
        //        Console.WriteLine("Greeting: " + replies.ResponseStream.Current.Message);
        //    }
        //}
    }
}
