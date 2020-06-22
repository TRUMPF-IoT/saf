// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SAF.Common;
using SAF.Services.SampleService1.AnyOtherInternalLogic;
using SAF.Services.SampleService1.MessageHandlers;
using SAF.Toolbox.Serialization;

namespace SAF.Services.SampleService1
{
    internal class MySpecialService : IHostedService
    {
        private readonly ILogger _log;
        private readonly IMessagingInfrastructure _messaging;
        private readonly MyServiceConfiguration _config;
        private Timer _timer;
        private long _pingId;

        private readonly List<object> _subscriptions = new List<object>();

        public MySpecialService(ILogger log, MyInternalDependency internalDependency, IMessagingInfrastructure messaging, MyServiceConfiguration serviceConfig)
        {
            _log = log ?? NullLogger<MySpecialService>.Instance;
            _messaging = messaging;
            _config = serviceConfig;

            internalDependency.SayHello();
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
                        _log.LogInformation($"Received {m.Topic} ({req.Id}), answering with pong/response");
                        _messaging.Publish(new Message
                        {
                            Topic = "pong/response", Payload = JsonSerializer.Serialize(new { ReplyTo = "ping/request", req.Id })
                        });
                    })
            });

            _log.LogInformation("My special service started.");
            _timer = new Timer(s =>
            {
                var pingId = Interlocked.Increment(ref _pingId);
                var payload = JsonSerializer.Serialize(new PingRequest { ReplyTo = "ping/response", Id = $"{pingId}" });
                _messaging.Publish(new Message { Topic = "ping/request", Payload = payload });

            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }

        public void Stop()
        {
            _log.LogInformation("My special service stopped.");
            _timer?.Dispose();
            UnsubscribeAll();
        }

        public void Kill()
        {
            _log.LogWarning("My special service killed!");
            _timer?.Dispose();
            UnsubscribeAll();
        }

        private void UnsubscribeAll()
        {
            foreach(var subscription in _subscriptions)
                _messaging?.Unsubscribe(subscription);

            _subscriptions.Clear();
        }
    }
}