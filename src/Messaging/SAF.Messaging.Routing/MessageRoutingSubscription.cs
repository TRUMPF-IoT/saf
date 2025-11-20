// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Routing;
using Common;

internal sealed class MessageRoutingSubscription : IDisposable
{
    private readonly IMessagingInfrastructure _messaging;
    private object[]? _subscriptionHandles;

    public MessageRoutingSubscription(IMessagingInfrastructure messaging)
    {
        _messaging = messaging;
    }

    public void Subscribe(string[] patterns, Action<Message> handler)
    {
        _subscriptionHandles = patterns.Select(p => _messaging.Subscribe(p, handler)).ToArray();
    }

    public void Subscribe<TMessageHandler>(string[] patterns) where TMessageHandler : IMessageHandler
    {
        _subscriptionHandles = patterns.Select(p => _messaging.Subscribe<TMessageHandler>(p)).ToArray();
    }

    public void Dispose()
    {
        if (_subscriptionHandles == null) return;
        Array.ForEach(_subscriptionHandles, _messaging.Unsubscribe);
        _subscriptionHandles = null;
    }
}