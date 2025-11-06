// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using SAF.Common;
using SAF.Communication.PubSub.Interfaces;

namespace SAF.Communication.PubSub.Cde;

/// <summary>
/// Defines the pattern and the handler to be executed for a subscription. 
/// </summary>
internal class SubscriptionInternal : AbstractSubscription, ISubscriptionInternal
{
    private readonly Subscriber _subscriber;
    private Action<string, TheProcessMessage>? _rawHandler;

    public SubscriptionInternal(Subscriber subscriber, params string[] patterns)
        : this(subscriber, RoutingOptions.All, patterns)
    { }

    public SubscriptionInternal(Subscriber subscriber, RoutingOptions routingOptions, params string[] patterns) : base(subscriber, routingOptions, patterns)
    {
        _subscriber = subscriber;
        _subscriber.MessageEvent += OnMessage;
    }

    public override void Unsubscribe()
    {
        base.Unsubscribe();
        _subscriber.MessageEvent -= OnMessage;
        _rawHandler = null;
    }

    public void SetRawHandler(Action<string, TheProcessMessage> callback)
    {
        _rawHandler = callback;
    }

    private void OnMessage(string topic, string msgVersion, TheProcessMessage msg)
    {
        if (Handler == null && _rawHandler == null) return;
        if (!msg.Message.IsRoutingAllowed(RoutingOptions)) return;
        
        var messageVersion = Version.Parse(msgVersion);
        if (messageVersion < Version.Parse(PubSubVersion.V4))
        {
            if (!IsTopicMatch(topic)) return;

            var message = msgVersion == PubSubVersion.V1
                ? new Message {Topic = topic, Payload = msg.Message.PLS}
                : TheCommonUtils.DeserializeJSONStringToObject<Message>(msg.Message.PLS);

            Handler?.Invoke(msg.Message.TIM, message);
            _rawHandler?.Invoke(msgVersion, msg);
        }
        else
        {
            var messages = TheCommonUtils.DeserializeJSONStringToObject<List<Message>>(msg.Message.PLS);
            messages.ForEach(m =>
            {
                if (!IsTopicMatch(m.Topic)) return;
                Handler?.Invoke(msg.Message.TIM, m);
            });
        }
    }
}