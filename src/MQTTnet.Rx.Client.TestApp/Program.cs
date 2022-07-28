// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using MQTTnet.Client;
using MQTTnet.Rx.ManagedClient;

namespace MQTTnet.Rx.Client.TestApp
{
    internal static class Program
    {
        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        /// <param name="args">The arguments.</param>
        private static void Main(string[] args)
        {
            var message = new Subject<(string topic, string payload)>();

            if (false)
            {
                Create.MqttClient().WithClientOptions(a => a.WithTcpServer("localhost", 9000)).PublishMessage(message)
                .Subscribe(r => Console.WriteLine($"{r.ReasonCode} [{r.PacketIdentifier}]"));
            }
            else
            {
                Create.ManagedMqttClient()
                .WithManagedClientOptions(a =>
                    a.WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                        .WithClientOptions(new MqttClientOptionsBuilder()
                        .WithTcpServer("localhost", 9000))).PublishMessage(message)
                .Subscribe(r => Console.WriteLine($"{r.ApplicationMessage.Id} [{r.ApplicationMessage.ApplicationMessage.Topic}]"));
            }

            Observable.Interval(TimeSpan.FromMilliseconds(10))
                    .Subscribe(i => message.OnNext(("samples/temperature/living_room", $"payload {i}")));
            Console.ReadLine();
        }
    }
}
