// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using SAF.Common;
using SAF.Toolbox.Serialization;

namespace SAF.Services.SampleService2
{
    internal class PingRequest
    {
        public string ReplyTo { get; set; }
        public string Id { get; set; }
    }

    public class MyService : IHostedService
    {
        private readonly ILogger<MyService> _log;
        private readonly IMessagingInfrastructure _messaging;
        private readonly List<object> _subscriptions = new();
        private Timer _timer;
        private long _pingId;

        public MyService(ILogger<MyService> log,
            IMessagingInfrastructure messaging)
        {
            _log = log;
            _messaging = messaging;
        }

        public void Start()
        {
            Action<Message> handler = m =>
            {
                var req = JsonSerializer.Deserialize<PingRequest>(m.Payload);
                _log.LogInformation($"Received {m.Topic} ({req.Id}), Payload: {m.Payload}");
            };
            _subscriptions.AddRange(new[]
            {
                _messaging.Subscribe("ping/response", handler),
                _messaging.Subscribe("pong/response", handler)
            });
            _log.LogInformation("My service started.");

            _timer = new Timer(s =>
            {
                _log.LogInformation($"-------------------------------------------------------------------------------");
                var pingId = Interlocked.Increment(ref _pingId);
                var payload = JsonSerializer.Serialize(new PingRequest { ReplyTo = "ping/response", Id = $"{pingId}" });
                _log.LogInformation($"Publish ping/request ({pingId}), Payload: {payload}");
                _messaging.Publish(new Message
                {
                    Topic = "ping/request",
                    Payload = payload,
                    CustomProperties = new List<MessageCustomProperty>
                {
                    new() {Name = "pingService", Value = nameof(MyService)},
                    new() {Name = "stringSetting", Value = ""}
                }
                });

            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }

        public void Stop()
        {
            _log.LogInformation("My service stopped.");
        }

        public void Kill()
        {
            _log.LogInformation("My service killed!");
        }
    }
}