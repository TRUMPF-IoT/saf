// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using System;
using System.Linq;
using SAF.Communication.PubSub.Interfaces;

namespace SAF.Communication.PubSub
{
    public abstract class AbstractSubscription : ISubscription
    {
        protected ISubscriber Subscriber { get; }

        protected Action<DateTimeOffset, string, string> Callback { get; private set; }

        public Guid Id { get; } = Guid.NewGuid();

        public RoutingOptions RoutingOptions { get; }

        public string[] Patterns { get; }

        protected AbstractSubscription(ISubscriber subscriber, params string[] patterns)
            : this(subscriber, RoutingOptions.All, patterns)
        { }

        protected AbstractSubscription(ISubscriber subscriber, RoutingOptions routingOptions, params string[] patterns)
        {
            Subscriber = subscriber;
            RoutingOptions = routingOptions;
            Patterns = patterns;
        }

        public void With(Action<DateTimeOffset, string, string> callback)
        {
            Callback = callback;
        }

        public virtual void Unsubscribe()
        {
            Subscriber.Unsubscribe(this);
            Callback = null;
        }

        protected bool IsTopicMatch(string topic) 
            => Patterns.Any(p => p == "*" || p == topic) || Patterns.Any(topic.IsMatch);

        public void Dispose()
        {
            Unsubscribe();
        }
    }
}