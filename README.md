![License](https://img.shields.io/github/license/ChrisPulman/MQTTnet.Rx.ManagedClient.svg) [![Build](https://github.com/ChrisPulman/MQTTnet.Rx.ManagedClient/actions/workflows/BuildOnly.yml/badge.svg)](https://github.com/ChrisPulman/MQTTnet.Rx.ManagedClient/actions/workflows/BuildOnly.yml) ![Nuget](https://img.shields.io/nuget/dt/MQTTnet.Rx.Client?color=pink&style=plastic) [![NuGet](https://img.shields.io/nuget/v/MQTTnet.Rx.Client.svg?style=plastic)](https://www.nuget.org/packages/MQTTnet.Rx.Client)

<p align="left">
  <a href="https://github.com/ChrisPulman/MQTTnet.Rx.ManagedClient">
    <img alt="MQTTnet.Rx.ManagedClient" src="https://github.com/ChrisPulman/MQTTnet.Rx.ManagedClient/blob/main/Images/logo.png" width="200"/>
  </a>
</p>


# MQTTnet.Rx.ManagedClient
A Reactive Managed Client for MQTTnet Broker

## Create a Mqtt Client to Publish an Observable stream
```csharp
Create.MqttClient()
    .WithClientOptions(a => a.WithTcpServer("localhost", 9000))
    .PublishMessage(_message)
    .Subscribe(r => Console.WriteLine($"{r.ReasonCode} [{r.PacketIdentifier}]"));
```

## Create a Managed Mqtt Client to Publish an Observable stream
```csharp
Create.ManagedMqttClient()
    .WithManagedClientOptions(a =>
    a.WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
        .WithClientOptions(c =>
            c.WithTcpServer("localhost", 9000)))
    .SubscribeToTopic("FromMilliseconds")
    .Subscribe(r => Console.WriteLine($"{r.ReasonCode} [{r.ApplicationMessage.Topic}] value : {r.ApplicationMessage.ConvertPayloadToString()}"));
```

## Create a Mqtt Client to Subscribe to a Topic
```csharp
Create.MqttClient()
    .WithClientOptions(a => a.WithTcpServer("localhost", 9000))
    .SubscribeToTopic("FromMilliseconds")
    .Subscribe(r => Console.WriteLine($"{r.ReasonCode} [{r.ApplicationMessage.Topic}] value : {r.ApplicationMessage.ConvertPayloadToString()}"));
```

## Create a Managed Mqtt Client to Subscribe to a Topic
```csharp
Create.ManagedMqttClient()
    .WithManagedClientOptions(a =>
        a.WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithClientOptions(c =>
                c.WithTcpServer("localhost", 9000)))
    .SubscribeToTopic("FromMilliseconds")
    .Subscribe(r => Console.WriteLine($"{r.ReasonCode} [{r.ApplicationMessage.Topic}] value : {r.ApplicationMessage.ConvertPayloadToString()}"));
```