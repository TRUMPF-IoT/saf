// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using SAF.Common;
using SAF.Communication.PubSub.Interfaces;

namespace SAF.Communication.PubSub
{
    public abstract class AbstractSubscription : ISubscription
    {
        protected ISubscriber Subscriber { get; }

        protected Action<DateTimeOffset, Message>? Handler { get; private set; }

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

        public void SetHandler(Action<DateTimeOffset, Message> handler)
        {
            Handler = handler;
        }

        public virtual void Unsubscribe()
        {
            Subscriber.Unsubscribe(this);
            Handler = null;
        }

        protected bool IsTopicMatch(string topic) 
            => Array.Exists(Patterns, p => p == "*" || p == topic) || Array.Exists(Patterns, topic.IsMatch);

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
    }
}