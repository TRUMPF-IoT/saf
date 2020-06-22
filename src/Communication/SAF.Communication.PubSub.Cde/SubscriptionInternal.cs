// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using nsCDEngine.ViewModels;
using SAF.Communication.PubSub.Interfaces;

namespace SAF.Communication.PubSub.Cde
{
    internal class SubscriptionInternal : AbstractSubscription, ISubscriptionInternal
    {
        private readonly Subscriber _subscriber;
        private Action<TheProcessMessage> _rawCallback;

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

        public void With(Action<TheProcessMessage> callback)
        {
            _rawCallback = callback;
        }

        private void OnMessage(string topic, TheProcessMessage msg)
        {
            if (Callback == null && _rawCallback == null) return;
            if (!msg.Message.IsRoutingAllowed(RoutingOptions)) return;
            if (!IsTopicMatch(topic)) return;

            Callback?.Invoke(msg.Message.TIM, topic, msg.Message.PLS);
            _rawCallback?.Invoke(msg);
        }
    }
}
