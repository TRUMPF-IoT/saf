// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SAF.Common;
using SAF.Services.SampleService1.AnyOtherInternalLogic;
using SAF.Services.SampleService1.MessageHandlers;
using SAF.Toolbox.Serialization;

namespace SAF.Services.SampleService1
{
    internal class MySpecialService : IHostedService
    {
        private readonly ILogger<MySpecialService> _log;
        private readonly IMessagingInfrastructure _messaging;
        private readonly MyServiceConfiguration _config;
        private readonly IConfiguration _hostConfig;

        private readonly List<object> _subscriptions = new();

        public MySpecialService(ILogger<MySpecialService> log,
            MyInternalDependency internalDependency,
            IMessagingInfrastructure messaging,
            MyServiceConfiguration serviceConfig,
            IOptionsMonitor<MyServiceConfiguration> monitoredConfig,
            IConfiguration hostConfig)
        {
            _log = log ?? NullLogger<MySpecialService>.Instance;
            _messaging = messaging;
            _config = serviceConfig;
            _hostConfig = hostConfig;

            internalDependency.SayHello();

            monitoredConfig.OnChange(OnServiceConfigurationChanged);
        }

        public void Start()
        {
            _subscriptions.AddRange(new[]
            {
                _messaging.Subscribe<CatchAllMessageHandler>(),
                _messaging.Subscribe<PingMessageHandler>("ping/request"),
                _messaging.Subscribe("ping/request", m =>
                    {
                        var req = JsonSerializer.Deserialize<PingRequest>(m.Payload);
                        _log.LogInformation($"Received {m.Topic} ({req.Id}), Payload: {m.Payload},\r\nanswering with pong/response regardless of the payload");
                        _messaging.Publish(new Message
                        {
                            Topic = "pong/response", Payload = JsonSerializer.Serialize(new { req.Id })
                        });
                    })
            });

            _log.LogInformation("My special service started.");
        }

        public void Stop()
        {
            _log.LogInformation("My special service stopped.");
            UnsubscribeAll();
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

        private void OnServiceConfigurationChanged(MyServiceConfiguration newConfig, string optionName)
        {
            _log.LogInformation($"Service configuration changed: {newConfig.MyNumericSetting}, {newConfig.MyStringSetting}");
        }
    }
}