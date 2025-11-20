// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Routing;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Common;

internal sealed class RoutingSubscription : IDisposable
{
    public IList<IDisposable> Subscriptions { get; } =
        new List<IDisposable>();

    public void Dispose()
    {
        foreach (var disposable in Subscriptions)
            disposable.Dispose();
    }
}

internal sealed class Messaging : IRoutingMessagingInfrastructure
{
    private readonly ILogger<Messaging> _log;
    private readonly IMessageRouting[] _messageRoutings;

    private readonly ConcurrentDictionary<Guid, (string pattern, IDisposable disposable)> _subscriptions = new();

    public Messaging(ILogger<Messaging>? log, IMessageRouting[] messageRoutings)
    {
        _log = log ?? NullLogger<Messaging>.Instance;
        _messageRoutings = messageRoutings;
    }

    public void Publish(Message message)
    {
        foreach (var routing in _messageRoutings)
            routing.Publish(message);
    }

    public object Subscribe<TMessageHandler>() where TMessageHandler : IMessageHandler => Subscribe<TMessageHandler>("*");

    public object Subscribe<TMessageHandler>(string routeFilterPattern) where TMessageHandler : IMessageHandler
    {
        var subscriptionId = Guid.NewGuid();

        var subscription = new RoutingSubscription();
        foreach (var routing in _messageRoutings)
        {
            var routingSub = routing.Subscribe<TMessageHandler>(routeFilterPattern);
            if (routingSub == null) continue;
            subscription.Subscriptions.Add(routingSub);
        }

        if (!_subscriptions.TryAdd(subscriptionId, (routeFilterPattern, subscription)))
            throw new ArgumentException("An element with the same key already exists!");

        return subscriptionId;
    }

    public object Subscribe(Action<Message> handler) => Subscribe("*", handler);

    public object Subscribe(string routeFilterPattern, Action<Message> handler)
    {
        var subscriptionId = Guid.NewGuid();

        var subscription = new RoutingSubscription();
        foreach (var routing in _messageRoutings)
        {
            var routingSub = routing.Subscribe(routeFilterPattern, handler);
            if (routingSub == null) continue;
            subscription.Subscriptions.Add(routingSub);
        }

        if (!_subscriptions.TryAdd(subscriptionId, (routeFilterPattern, subscription)))
            throw new ArgumentException("An element with the same key already exists!");

        return subscriptionId;
    }

    public void Unsubscribe(object subscription)
    {
        var subscriptionId = subscription as Guid?;
        if (!subscriptionId.HasValue)
        {
            _log.LogWarning($"Unsubscribe failed. Invalid subscription object passed: \"{subscription}\".");
            return;
        }

        if (!_subscriptions.TryRemove(subscriptionId.Value, out var sub))
        {
            _log.LogWarning($"Unsubscribe failed. Subscription not active anymore: \"{subscriptionId}\".");
            return;
        }

        sub.disposable.Dispose();
        _log.LogDebug($"Unsubscribed subscription \"{subscriptionId}\" for channel \"{sub.pattern}\"");
    }
}