// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Concurrent;
using System.Threading;
using SAF.Common;

namespace SAF.DevToolbox.TestRunner;

public abstract class TestSequenceBase : IMessageHandler
{
    private readonly IMessagingInfrastructure _messaging;
    private readonly ConcurrentDictionary<string, Action<string?>> _persistActions = new();

    internal Action<string>? TraceTitleAction { get; set; }
    internal Action<string, string>? TraceDocumentationAction { get; set; }
        
    public bool CanHandle(Message message) => true;

    protected TestSequenceBase(IMessagingInfrastructure messaging)
    {
        _messaging = messaging;
    }

    public void Handle(Message message)
    {
        if (!_persistActions.TryGetValue(message.Topic, out var action))
            return;

        action(message.Payload);
    }

    protected void WaitForValueSet(ref string? value, int timeoutSeconds)
    {
        var c = 0;
        while (value == null && ++c <= timeoutSeconds * 5)
            Thread.Sleep(200);

        if (value == null)
            throw new TimeoutException();
    }

    protected IDisposable PayloadToVariable<T>(string topic, Action<string?> persistAction) where T : IMessageHandler
    {
        _persistActions.AddOrUpdate(topic, persistAction, (_, _) => persistAction);
        var subscribeId = _messaging.Subscribe<T>(topic);
        return new DisposableMessagingSubscription(_messaging, subscribeId);
    }

    protected IDisposable PayloadToVariable(string topic, Action<string?> persistAction)
    {
        var subscribeId = _messaging.Subscribe(topic, msg => persistAction(msg.Payload));
        return new DisposableMessagingSubscription(_messaging, subscribeId);
    }

    protected void TraceDocumentation(string title, string doc) => TraceDocumentationAction?.Invoke(title, doc);

    protected void TraceTitle(string title) => TraceTitleAction?.Invoke(title);

    public abstract void Run();

    private sealed class DisposableMessagingSubscription : IDisposable
    {
        private readonly IMessagingInfrastructure _messaging;
        private readonly object _subscriptionId;

        public DisposableMessagingSubscription(IMessagingInfrastructure messaging, object subscriptionId)
        {
            _messaging = messaging;
            _subscriptionId = subscriptionId;
        }

        public void Dispose() => _messaging.Unsubscribe(_subscriptionId);
    }
}