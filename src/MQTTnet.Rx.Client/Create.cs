// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;

namespace MQTTnet.Rx.ManagedClient
{
    /// <summary>
    /// Create.
    /// </summary>
    public static class Create
    {
        /// <summary>
        /// Created a mqtt Client.
        /// </summary>
        /// <returns>An IMqttClient.</returns>
        public static IObservable<IMqttClient> MqttClient() =>
            Observable.Create<IMqttClient>(observer =>
                {
                    var mqttClient = new MqttFactory().CreateMqttClient();
                    observer.OnNext(mqttClient);
                    return Disposable.Create(() => mqttClient.Dispose());
                }).Retry();

        /// <summary>
        /// Manageds the MQTT client.
        /// </summary>
        /// <returns>A Managed Mqtt Client.</returns>
        public static IObservable<IManagedMqttClient> ManagedMqttClient() =>
            Observable.Create<IManagedMqttClient>(observer =>
                {
                    var mqttClient = new MqttFactory().CreateManagedMqttClient();
                    observer.OnNext(mqttClient);
                    return Disposable.Create(() => mqttClient.Dispose());
                }).Retry();

        /// <summary>
        /// Withes the client options.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="optionsBuilder">The options builder.</param>
        /// <returns>A Mqtt Client and client Options.</returns>
        public static IObservable<IMqttClient> WithClientOptions(this IObservable<IMqttClient> client, Action<MqttClientOptionsBuilder> optionsBuilder) =>
            Observable.Create<IMqttClient>(observer =>
            {
                var mqttClientOptions = new MqttClientOptionsBuilder();
                optionsBuilder(mqttClientOptions);
                return client.Subscribe(async c =>
                {
                    await c.ConnectAsync(mqttClientOptions.Build(), CancellationToken.None);
                    observer.OnNext(c);
                });
            });

        /// <summary>
        /// Withes the managed client options.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="optionsBuilder">The options builder.</param>
        /// <returns>A Managed Mqtt Client.</returns>
        public static IObservable<IManagedMqttClient> WithManagedClientOptions(this IObservable<IManagedMqttClient> client, Action<ManagedMqttClientOptionsBuilder> optionsBuilder) =>
            Observable.Create<IManagedMqttClient>(observer =>
            {
                var mqttClientOptions = new ManagedMqttClientOptionsBuilder();
                optionsBuilder(mqttClientOptions);
                return client.Subscribe(async c =>
                {
                    await c.StartAsync(mqttClientOptions.Build());
                    observer.OnNext(c);
                });
            });

        /// <summary>
        /// Publishes the message.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="message">The message.</param>
        /// <param name="qos">The QoS.</param>
        /// <param name="retain">if set to <c>true</c> [retain].</param>
        /// <returns>
        /// A Mqtt Client Publish Result.
        /// </returns>
        public static IObservable<MqttClientPublishResult> PublishMessage(this IObservable<IMqttClient> client, IObservable<(string topic, string payLoad)> message, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.ExactlyOnce, bool retain = true) =>
            Observable.Create<MqttClientPublishResult>(observer =>
            {
                return client.CombineLatest(message, (cli, mess) => (cli, mess)).Subscribe(async c =>
                {
                    var applicationMessage = new MqttApplicationMessageBuilder()
                                    .WithTopic(c.mess.topic)
                                    .WithPayload(c.mess.payLoad)
                                    .WithQualityOfServiceLevel(qos)
                                    .WithRetainFlag(retain)
                                    .Build();

                    var result = await c.cli.PublishAsync(applicationMessage, CancellationToken.None);
                    observer.OnNext(result);
                });
            }).Retry();

        /// <summary>
        /// Publishes the message.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="message">The message.</param>
        /// <param name="qos">The qos.</param>
        /// <param name="retain">if set to <c>true</c> [retain].</param>
        /// <returns>A Mqtt Client Publish Result.</returns>
        public static IObservable<ApplicationMessageProcessedEventArgs> PublishMessage(this IObservable<IManagedMqttClient> client, IObservable<(string topic, string payLoad)> message, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.ExactlyOnce, bool retain = true) =>
            Observable.Create<ApplicationMessageProcessedEventArgs>(observer =>
            {
                var disposable = new CompositeDisposable();
                var setup = false;
                disposable.Add(client.CombineLatest(message, (cli, mess) => (cli, mess)).Subscribe(async c =>
                {
                    if (!setup)
                    {
                        setup = true;
                        disposable.Add(c.cli.ApplicationMessageProcessed().Retry().Subscribe(args => observer.OnNext(args)));
                    }

                    var applicationMessage = new MqttApplicationMessageBuilder()
                                    .WithTopic(c.mess.topic)
                                    .WithPayload(c.mess.payLoad)
                                    .WithQualityOfServiceLevel(qos)
                                    .WithRetainFlag(retain)
                                    .Build();

                    await c.cli.EnqueueAsync(applicationMessage);
                }));

                return disposable;
            }).Retry();

        /// <summary>
        /// Publishes the message.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="message">The message.</param>
        /// <param name="messageBuilder">The message builder.</param>
        /// <param name="qos">The qos.</param>
        /// <param name="retain">if set to <c>true</c> [retain].</param>
        /// <returns>A Mqtt Client Publish Result.</returns>
        public static IObservable<MqttClientPublishResult> PublishMessage(this IObservable<IMqttClient> client, IObservable<(string topic, string payLoad)> message, Action<MqttApplicationMessageBuilder> messageBuilder, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.ExactlyOnce, bool retain = true) =>
            Observable.Create<MqttClientPublishResult>(observer =>
            {
                return client.CombineLatest(message, (cli, mess) => (cli, mess)).Subscribe(async c =>
                {
                    var applicationMessage = new MqttApplicationMessageBuilder()
                                    .WithTopic(c.mess.topic)
                                    .WithPayload(c.mess.payLoad)
                                    .WithQualityOfServiceLevel(qos)
                                    .WithRetainFlag(retain);
                    messageBuilder(applicationMessage);

                    var result = await c.cli.PublishAsync(applicationMessage.Build(), CancellationToken.None);
                    observer.OnNext(result);
                });
            }).Retry();

        /// <summary>
        /// Publishes the message.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="message">The message.</param>
        /// <param name="qos">The qos.</param>
        /// <param name="retain">if set to <c>true</c> [retain].</param>
        /// <returns>A Mqtt Client Publish Result.</returns>
        public static IObservable<MqttClientPublishResult> PublishMessage(this IObservable<IMqttClient> client, IObservable<(string topic, byte[] payLoad)> message, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.ExactlyOnce, bool retain = true) =>
            Observable.Create<MqttClientPublishResult>(observer =>
            {
                return client.CombineLatest(message, (cli, mess) => (cli, mess)).Subscribe(async c =>
                {
                    var applicationMessage = new MqttApplicationMessageBuilder()
                                    .WithTopic(c.mess.topic)
                                    .WithPayload(c.mess.payLoad)
                                    .WithQualityOfServiceLevel(qos)
                                    .WithRetainFlag(retain)
                                    .Build();

                    var result = await c.cli.PublishAsync(applicationMessage, CancellationToken.None);
                    observer.OnNext(result);
                });
            }).Retry();

        /// <summary>
        /// Publishes the message.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="message">The message.</param>
        /// <param name="messageBuilder">The message builder.</param>
        /// <param name="qos">The qos.</param>
        /// <param name="retain">if set to <c>true</c> [retain].</param>
        /// <returns>A Mqtt Client Publish Result.</returns>
        public static IObservable<MqttClientPublishResult> PublishMessage(this IObservable<IMqttClient> client, IObservable<(string topic, byte[] payLoad)> message, Action<MqttApplicationMessageBuilder> messageBuilder, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.ExactlyOnce, bool retain = true) =>
            Observable.Create<MqttClientPublishResult>(observer =>
            {
                return client.CombineLatest(message, (cli, mess) => (cli, mess)).Subscribe(async c =>
                {
                    var applicationMessage = new MqttApplicationMessageBuilder()
                                    .WithTopic(c.mess.topic)
                                    .WithPayload(c.mess.payLoad)
                                    .WithQualityOfServiceLevel(qos)
                                    .WithRetainFlag(retain);
                    messageBuilder(applicationMessage);

                    var result = await c.cli.PublishAsync(applicationMessage.Build(), CancellationToken.None);
                    observer.OnNext(result);
                });
            }).Retry();

        /// <summary>
        /// Froms the asynchronous event.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <param name="addHandler">The add handler.</param>
        /// <param name="removeHandler">The remove handler.</param>
        /// <returns>Observable event.</returns>
        public static IObservable<T> FromAsyncEvent<T>(Action<Func<T, Task>> addHandler, Action<Func<T, Task>> removeHandler)
        {
            return Observable
                .Create<T>(observer =>
                {
                    Task Delegate(T args)
                    {
                        observer.OnNext(args);
                        return Task.CompletedTask;
                    }

                    addHandler(Delegate);
                    return Disposable.Create(() => removeHandler(Delegate));
                })
                .Publish()
                .RefCount();
        }

        /// <summary>
        /// Applications the message processed.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <returns>A Application Message Processed Event Args.</returns>
        public static IObservable<ApplicationMessageProcessedEventArgs> ApplicationMessageProcessed(this IManagedMqttClient client)
        {
            return FromAsyncEvent<ApplicationMessageProcessedEventArgs>(
                handler => client.ApplicationMessageProcessedAsync += handler,
                handler => client.ApplicationMessageProcessedAsync -= handler);
        }
    }
}
