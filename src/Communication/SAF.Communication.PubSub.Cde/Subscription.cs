// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde;
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using Common;
using Interfaces;

/// <summary>
/// Defines the pattern and the handler to be executed for a subscription. 
/// </summary>
internal class Subscription : ISubscription
{
    private readonly Subscriber _subscriber;
    private Action<DateTimeOffset, Message>? _handler;

    public Subscription(Subscriber subscriber, params string[] patterns)
        : this(subscriber, RoutingOptions.All, patterns)
    { }

    public Subscription(Subscriber subscriber, RoutingOptions routingOptions, params string[] patterns)
    {
        _subscriber = subscriber;
        RoutingOptions = routingOptions;
        Patterns = patterns;

        _subscriber.MessageEvent += OnMessage;
    }

    public Guid Id { get; } = Guid.NewGuid();
    public RoutingOptions RoutingOptions { get; }
    public string[] Patterns { get; }

    public void SetHandler(Action<DateTimeOffset, Message> handler)
    {
        _handler = handler;
    }

    public void Unsubscribe()
    {
        _subscriber.Unsubscribe(this);
        _subscriber.MessageEvent -= OnMessage;

        _handler = null;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        Unsubscribe();
    }

    private bool IsTopicMatch(string topic)
        => Array.Exists(Patterns, p => p == "*" || p == topic) || Array.Exists(Patterns, topic.IsMatch);

    private void OnMessage(string topic, string msgVersion, TheProcessMessage msg)
    {
        if (_handler == null) return;
        if (!msg.Message.IsRoutingAllowed(RoutingOptions)) return;
        
        var messageVersion = Version.Parse(msgVersion);
        if (messageVersion < Version.Parse(PubSubVersion.V4) || !topic.StartsWith("$$batch"))
        {
            if (!IsTopicMatch(topic)) return;

            var message = msgVersion == PubSubVersion.V1
                ? new Message {Topic = topic, Payload = msg.Message.PLS}
                : TheCommonUtils.DeserializeJSONStringToObject<Message>(msg.Message.PLS);

            _handler.Invoke(msg.Message.TIM, message);
        }
        else
        {
            var messages = TheCommonUtils.DeserializeJSONStringToObject<List<Message>>(msg.Message.PLS);
            messages.ForEach(m =>
            {
                if (!IsTopicMatch(m.Topic)) return;
                _handler.Invoke(msg.Message.TIM, m);
            });
        }
    }
}