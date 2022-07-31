// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;

namespace MQTTnet.Rx.Client
{
    /// <summary>
    /// Mqttd Subscribe Extensions.
    /// </summary>
    public static class MqttdSubscribeExtensions
    {
        /// <summary>
        /// Subscribes to topic.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="topic">The topic.</param>
        /// <returns>An Observable Mqtt Client Subscribe Result.</returns>
        public static IObservable<MqttApplicationMessageReceivedEventArgs> SubscribeToTopic(this IObservable<IMqttClient> client, string topic) =>
            Observable.Create<MqttApplicationMessageReceivedEventArgs>(observer =>
            {
                var disposable = new CompositeDisposable();
                IMqttClient? mqttClient = null;
                disposable.Add(client.Subscribe(async c =>
                {
                    mqttClient = c;
                    disposable.Add(mqttClient.ApplicationMessageReceived().Subscribe(observer));
                    var mqttSubscribeOptions = Create.MqttFactory.CreateSubscribeOptionsBuilder()
                        .WithTopicFilter(f => f.WithTopic(topic))
                        .Build();

                    await mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
                }));

                return Disposable.Create(async () =>
                    {
                        try
                        {
                            await mqttClient!.UnsubscribeAsync(topic).ConfigureAwait(false);
                            disposable.Dispose();
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                        catch (Exception exception)
                        {
                            observer.OnError(exception);
                        }
                    });
            }).Retry();

        /// <summary>
        /// Subscribes to topic.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="topic">The topic.</param>
        /// <returns>An Observable Mqtt Client Subscribe Result.</returns>
        public static IObservable<MqttApplicationMessageReceivedEventArgs> SubscribeToTopic(this IObservable<IManagedMqttClient> client, string topic) =>
            Observable.Create<MqttApplicationMessageReceivedEventArgs>(observer =>
            {
                var disposable = new CompositeDisposable();
                IManagedMqttClient? mqttClient = null;
                disposable.Add(client.Subscribe(async c =>
                {
                    mqttClient = c;
                    disposable.Add(mqttClient.ApplicationMessageReceived().Subscribe(observer));
                    var mqttSubscribeOptions = Create.MqttFactory.CreateTopicFilterBuilder()
                        .WithTopic(topic)
                        .Build();

                    await mqttClient.SubscribeAsync(new[] { mqttSubscribeOptions });
                }));

                return Disposable.Create(async () =>
                    {
                        try
                        {
                            await mqttClient!.UnsubscribeAsync(new[] { topic }).ConfigureAwait(false);
                            disposable.Dispose();
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                        catch (Exception exception)
                        {
                            observer.OnError(exception);
                        }
                    });
            }).Retry().Publish().RefCount();
    }
}
