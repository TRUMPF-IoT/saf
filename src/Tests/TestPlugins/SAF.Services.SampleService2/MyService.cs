// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Services.SampleService2;
using Microsoft.Extensions.Logging;
using Common;
using SAF.PluginSystem.Hosting.Contracts;
using Toolbox.Serialization;

internal class PingRequest
{
    public string ReplyTo { get; set; } = default!;
    public string Id { get; set; } = default!;
}

public class MyService : IServicePlugin
{
    private readonly ILogger<MyService> _log;
    private readonly IMessagingInfrastructure _messaging;
    private readonly List<object> _subscriptions = new();
    private Timer? _timer;
    private long _pingId;

    public MyService(ILogger<MyService> log,
        IMessagingInfrastructure messaging)
    {
        _log = log;
        _messaging = messaging;
    }

    public Task StartAsync(CancellationToken token)
    {
        void Handler(Message m)
        {
            var req = m.Payload != null ? JsonSerializer.Deserialize<PingRequest>(m.Payload) : null;
            _log.LogInformation($"Received {m.Topic} ({req?.Id}), Payload: {m.Payload}");
        }

        _subscriptions.AddRange(new[]
        {
            _messaging.Subscribe("ping/response", Handler),
            _messaging.Subscribe("pong/response", Handler)
        });
        _log.LogInformation("My service started.");

        _timer = new Timer(_ =>
        {
            _log.LogInformation("-------------------------------------------------------------------------------");

            var pingId = Interlocked.Increment(ref _pingId);
            var payload = JsonSerializer.Serialize(new PingRequest {ReplyTo = "ping/response", Id = $"{pingId}"});

            _log.LogInformation($"Publish ping/request ({pingId}), Payload: {payload}");
            _messaging.Publish(new Message
            {
                Topic = "ping/request",
                Payload = payload,
                CustomProperties =
                [
                    new MessageCustomProperty {Name = "pingService", Value = nameof(MyService)},
                    new MessageCustomProperty {Name = "stringSetting", Value = ""}
                ]
            });

        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token)
    {
        foreach (var subscription in _subscriptions)
        {
            _messaging.Unsubscribe(subscription);
        }
        _subscriptions.Clear();

        _timer?.Dispose();
        _log.LogInformation("My service stopped.");

        return Task.CompletedTask;
    }

    public void Kill()
    {
        _ = StopAsync(CancellationToken.None);

        _log.LogInformation("My service killed.");
    }
}