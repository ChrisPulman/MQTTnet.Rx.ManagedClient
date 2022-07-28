// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;

namespace MQTTnet.Rx.Client
{
    /// <summary>
    /// Create.
    /// </summary>
    public static class Create
    {
        /// <summary>
        /// Gets the MQTT factory.
        /// </summary>
        /// <value>
        /// The MQTT factory.
        /// </value>
        public static MqttFactory MqttFactory { get; } = new();

        /// <summary>
        /// Created a mqtt Client.
        /// </summary>
        /// <returns>An IMqttClient.</returns>
        public static IObservable<IMqttClient> MqttClient() =>
            Observable.Create<IMqttClient>(observer =>
                {
                    var mqttClient = MqttFactory.CreateMqttClient();
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
                    var mqttClient = MqttFactory.CreateManagedMqttClient();
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
                var mqttClientOptions = MqttFactory.CreateClientOptionsBuilder();
                optionsBuilder(mqttClientOptions);
                var disposable = new CompositeDisposable();
                disposable.Add(client.Subscribe(c => disposable.Add(Observable.StartAsync(async token => await c.ConnectAsync(mqttClientOptions.Build(), token)).Subscribe(_ => observer.OnNext(c)))));
                return disposable;
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
                var mqttClientOptions = MqttFactory.CreateManagedClientOptionsBuilder();
                optionsBuilder(mqttClientOptions);
                var disposable = new CompositeDisposable();
                disposable.Add(client.Subscribe(c => disposable.Add(Observable.StartAsync(async () => await c.StartAsync(mqttClientOptions.Build())).Subscribe(_ => observer.OnNext(c)))));
                return disposable;
            });

        /// <summary>
        /// Withes the client options.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="clientBuilder">The client builder.</param>
        /// <returns>A ManagedMqttClientOptionsBuilder.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// builder
        /// or
        /// clientBuilder.
        /// </exception>
        public static ManagedMqttClientOptionsBuilder WithClientOptions(this ManagedMqttClientOptionsBuilder builder, Action<MqttClientOptionsBuilder> clientBuilder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (clientBuilder == null)
            {
                throw new ArgumentNullException(nameof(clientBuilder));
            }

            var optionsBuilder = MqttFactory.CreateClientOptionsBuilder();
            clientBuilder(optionsBuilder);
            builder.WithClientOptions(optionsBuilder);
            return builder;
        }

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
                    var mqttSubscribeOptions = MqttFactory.CreateSubscribeOptionsBuilder()
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
                    var mqttSubscribeOptions = MqttFactory.CreateTopicFilterBuilder()
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
                client.CombineLatest(message, (cli, mess) => (cli, mess)).Subscribe(async c =>
                {
                    var applicationMessage = MqttFactory.CreateApplicationMessageBuilder()
                                    .WithTopic(c.mess.topic)
                                    .WithPayload(c.mess.payLoad)
                                    .WithQualityOfServiceLevel(qos)
                                    .WithRetainFlag(retain)
                                    .Build();

                    var result = await c.cli.PublishAsync(applicationMessage, CancellationToken.None);
                    observer.OnNext(result);
                })).Retry();

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

                    var applicationMessage = MqttFactory.CreateApplicationMessageBuilder()
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
        /// <param name="qos">The qos.</param>
        /// <param name="retain">if set to <c>true</c> [retain].</param>
        /// <returns>A Mqtt Client Publish Result.</returns>
        public static IObservable<ApplicationMessageProcessedEventArgs> PublishMessage(this IObservable<IManagedMqttClient> client, IObservable<(string topic, byte[] payLoad)> message, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.ExactlyOnce, bool retain = true) =>
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

                    var applicationMessage = MqttFactory.CreateApplicationMessageBuilder()
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
                client.CombineLatest(message, (cli, mess) => (cli, mess)).Subscribe(async c =>
                {
                    var applicationMessage = MqttFactory.CreateApplicationMessageBuilder()
                                    .WithTopic(c.mess.topic)
                                    .WithPayload(c.mess.payLoad)
                                    .WithQualityOfServiceLevel(qos)
                                    .WithRetainFlag(retain);
                    messageBuilder(applicationMessage);

                    var result = await c.cli.PublishAsync(applicationMessage.Build(), CancellationToken.None);
                    observer.OnNext(result);
                })).Retry();

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
                client.CombineLatest(message, (cli, mess) => (cli, mess)).Subscribe(async c =>
                {
                    var applicationMessage = MqttFactory.CreateApplicationMessageBuilder()
                                    .WithTopic(c.mess.topic)
                                    .WithPayload(c.mess.payLoad)
                                    .WithQualityOfServiceLevel(qos)
                                    .WithRetainFlag(retain)
                                    .Build();

                    var result = await c.cli.PublishAsync(applicationMessage, CancellationToken.None);
                    observer.OnNext(result);
                })).Retry();

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
                client.CombineLatest(message, (cli, mess) => (cli, mess)).Subscribe(async c =>
                {
                    var applicationMessage = MqttFactory.CreateApplicationMessageBuilder()
                                    .WithTopic(c.mess.topic)
                                    .WithPayload(c.mess.payLoad)
                                    .WithQualityOfServiceLevel(qos)
                                    .WithRetainFlag(retain);
                    messageBuilder(applicationMessage);

                    var result = await c.cli.PublishAsync(applicationMessage.Build(), CancellationToken.None);
                    observer.OnNext(result);
                })).Retry();

        /// <summary>
        /// Creates the client options builder.
        /// </summary>
        /// <param name="factory">The MqttFactory.</param>
        /// <returns>A Managed Mqtt Client Options Builder.</returns>
        public static ManagedMqttClientOptionsBuilder CreateManagedClientOptionsBuilder(this MqttFactory factory) => new();

        /// <summary>
        /// Applications the message processed.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <returns>A Application Message Processed Event Args.</returns>
        public static IObservable<ApplicationMessageProcessedEventArgs> ApplicationMessageProcessed(this IManagedMqttClient client) =>
            FromAsyncEvent<ApplicationMessageProcessedEventArgs>(
                handler => client.ApplicationMessageProcessedAsync += handler,
                handler => client.ApplicationMessageProcessedAsync -= handler);

        /// <summary>
        /// Connecteds the specified client.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <returns>A Mqtt Client Connected Event Args.</returns>
        public static IObservable<MqttClientConnectedEventArgs> Connected(this IManagedMqttClient client) =>
            FromAsyncEvent<MqttClientConnectedEventArgs>(
                handler => client.ConnectedAsync += handler,
                handler => client.ConnectedAsync -= handler);

        /// <summary>
        /// Disconnecteds the specified client.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <returns>A Mqtt Client Disconnected Event Args.</returns>
        public static IObservable<MqttClientDisconnectedEventArgs> Disconnected(this IManagedMqttClient client) =>
            FromAsyncEvent<MqttClientDisconnectedEventArgs>(
                handler => client.DisconnectedAsync += handler,
                handler => client.DisconnectedAsync -= handler);

        /// <summary>
        /// Connectings the failed.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <returns>A Connecting Failed Event Args.</returns>
        public static IObservable<ConnectingFailedEventArgs> ConnectingFailed(this IManagedMqttClient client) =>
            FromAsyncEvent<ConnectingFailedEventArgs>(
                handler => client.ConnectingFailedAsync += handler,
                handler => client.ConnectingFailedAsync -= handler);

        /// <summary>
        /// Synchronizings the subscriptions failed.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <returns>A Managed Process Failed Event Args.</returns>
        public static IObservable<ManagedProcessFailedEventArgs> SynchronizingSubscriptionsFailed(this IManagedMqttClient client) =>
            FromAsyncEvent<ManagedProcessFailedEventArgs>(
                handler => client.SynchronizingSubscriptionsFailedAsync += handler,
                handler => client.SynchronizingSubscriptionsFailedAsync -= handler);

        /// <summary>
        /// Applications the message processed.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <returns>A Application Message Skipped Event Args.</returns>
        public static IObservable<ApplicationMessageSkippedEventArgs> ApplicationMessageSkipped(this IManagedMqttClient client) =>
            FromAsyncEvent<ApplicationMessageSkippedEventArgs>(
                handler => client.ApplicationMessageSkippedAsync += handler,
                handler => client.ApplicationMessageSkippedAsync -= handler);

        /// <summary>
        /// Applications the message received.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <returns>A Mqtt Application Message Received Event Args.</returns>
        public static IObservable<MqttApplicationMessageReceivedEventArgs> ApplicationMessageReceived(this IManagedMqttClient client) =>
            FromAsyncEvent<MqttApplicationMessageReceivedEventArgs>(
                handler => client.ApplicationMessageReceivedAsync += handler,
                handler => client.ApplicationMessageReceivedAsync -= handler);

        /// <summary>
        /// Applications the message received.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <returns>A Mqtt Application Message Received Event Args.</returns>
        public static IObservable<MqttApplicationMessageReceivedEventArgs> ApplicationMessageReceived(this IMqttClient client) =>
            FromAsyncEvent<MqttApplicationMessageReceivedEventArgs>(
                handler => client.ApplicationMessageReceivedAsync += handler,
                handler => client.ApplicationMessageReceivedAsync -= handler);

        internal static IObservable<T> FromAsyncEvent<T>(Action<Func<T, Task>> addHandler, Action<Func<T, Task>> removeHandler) =>
            Observable.Create<T>(observer =>
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
}
