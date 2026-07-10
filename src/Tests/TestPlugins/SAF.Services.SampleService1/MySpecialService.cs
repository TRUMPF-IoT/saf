// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Services.SampleService1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SAF.Common;
using SAF.Messaging.Contracts;
using SAF.PluginSystem.Hosting.Contracts;
using AnyOtherInternalLogic;
using MessageHandlers;
using Toolbox.Serialization;

internal class MySpecialService : IServicePlugin
{
    private readonly ILogger<MySpecialService> _log;
    private readonly IMessagingInfrastructure _messaging;
    private readonly MyServiceConfiguration _config;

    private readonly List<object> _subscriptions = new();

    public MySpecialService(ILogger<MySpecialService> log,
        MyInternalDependency internalDependency,
        IMessagingInfrastructure messaging,
        MyServiceConfiguration serviceConfig,
        IOptionsMonitor<MyServiceConfiguration> monitoredConfig)
    {
        _log = log;
        _messaging = messaging;
        _config = serviceConfig;

        internalDependency.SayHello();

        monitoredConfig.OnChange(OnServiceConfigurationChanged);
    }

    public Task StartAsync(CancellationToken cancelToken)
    {
        _subscriptions.AddRange(
        [
            _messaging.Subscribe<CatchAllMessageHandler>(),
            _messaging.Subscribe<PingMessageHandler>("ping/request"),
            _messaging.Subscribe("ping/request", m =>
            {
                if(m.Payload == null) return;

                var req = JsonSerializer.Deserialize<PingRequest>(m.Payload);
                if(req == null) return;

                _log.LogInformation($"Received {m.Topic} ({req.Id}), Payload: {m.Payload},\r\nanswering with pong/response regardless of the payload");
                _messaging.Publish(new Message
                {
                    Topic = "pong/response", Payload = JsonSerializer.Serialize(new { req.Id })
                });
            })
        ]);

        _log.LogInformation("My special service started.");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancelToken)
    {
        _log.LogInformation("My special service stopped.");
        UnsubscribeAll();

        return Task.CompletedTask;
    }

    public void Kill()
    {
        _log.LogWarning("My special service killed!");
        UnsubscribeAll();
    }

    private void UnsubscribeAll()
    {
        foreach(var subscription in _subscriptions)
            _messaging?.Unsubscribe(subscription);

        _subscriptions.Clear();
    }

    private void OnServiceConfigurationChanged(MyServiceConfiguration newConfig, string? optionName)
        => _log.LogInformation($"Service configuration changed: {newConfig.MyNumericSetting}, {newConfig.MyStringSetting}");
}


