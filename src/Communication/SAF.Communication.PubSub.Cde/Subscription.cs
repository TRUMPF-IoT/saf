// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde;
using nsCDEngine.ViewModels;
using SAF.Common;
using SAF.Messaging.Contracts;
using Interfaces;
using WireFormat;

/// <summary>
/// Defines the pattern and the handler to be executed for a subscription. 
/// </summary>
internal class Subscription : ISubscription
{
    private readonly Subscriber _subscriber;
    private readonly ITsmMessageDecoder _decoder;
    private Action<DateTimeOffset, Message>? _handler;

    public Subscription(Subscriber subscriber, ITsmMessageDecoder decoder, params string[] patterns)
        : this(subscriber, decoder, RoutingOptions.All, patterns)
    { }

    public Subscription(Subscriber subscriber, ITsmMessageDecoder decoder, RoutingOptions routingOptions, params string[] patterns)
    {
        _subscriber = subscriber;
        _decoder = decoder;
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

    private void OnMessage(string topic, string msgVersion, TheProcessMessage msg, IReadOnlyList<Message>? batchMessages)
    {
        if (_handler == null) return;
        if (!msg.Message.IsRoutingAllowed(RoutingOptions)) return;

        if (batchMessages == null)
        {
            if (!IsTopicMatch(topic)) return;

            _handler.Invoke(msg.Message.TIM, _decoder.Decode(topic, msgVersion, msg.Message.PLS)!);
            return;
        }

        // The batch is decoded once per subscriber; handlers must not change the messages.
        foreach (var message in batchMessages)
        {
            if (!IsTopicMatch(message.Topic)) continue;
            _handler.Invoke(msg.Message.TIM, message);
        }
    }
}


