// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using SAF.Common;
using SAF.Communication.PubSub.Interfaces;

namespace SAF.Communication.PubSub.Cde
{
    internal class SubscriptionInternal : AbstractSubscription, ISubscriptionInternal
    {
        private readonly Subscriber _subscriber;
        private Action<string, TheProcessMessage> _rawCallback;

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
            _rawCallback = null;
        }

        public void With(Action<string, TheProcessMessage> callback)
        {
            _rawCallback = callback;
        }

        private void OnMessage(string topic, string msgVersion, TheProcessMessage msg)
        {
            if (Callback == null && _rawCallback == null) return;
            if (!msg.Message.IsRoutingAllowed(RoutingOptions)) return;
            if (!IsTopicMatch(topic)) return;

            var message = msgVersion == PubSubVersion.V1
                ? new Message { Topic = topic, Payload = msg.Message.PLS }
                : TheCommonUtils.DeserializeJSONStringToObject<Message>(msg.Message.PLS);

            Callback?.Invoke(msg.Message.TIM, message);
            _rawCallback?.Invoke(msgVersion, msg);
        }
    }
}
