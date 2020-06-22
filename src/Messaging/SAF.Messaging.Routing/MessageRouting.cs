// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using SAF.Common;

namespace SAF.Messaging.Routing
{
    /// <summary>
    /// Describes a Pub/Sub message route for use with IServiceCollection.AddRoutingMessagingInfrastructure.
    /// </summary>
    public class MessageRouting
    {
        /// <summary>
        /// The patterns of message topics to publish to the Messaging infrastructure of this instance.
        /// </summary>
        public string[] PublishPatterns { get; set; }
        /// <summary>
        /// The patterns of message topics to subscribe to on the Messaging infrastructure of this instance.
        /// </summary>
        public string[] SubscriptionPatterns { get; set; }

        /// <summary>
        /// The IMessagingInfrastructure used to route messages from/to.
        /// </summary>
        public IMessagingInfrastructure Messaging { get; }

        /// <summary>
        /// Creates a MessageRouting instance.
        /// </summary>
        /// <param name="messaging">The IMessagingInfrastructure for which message routing is done.</param>
        public MessageRouting(IMessagingInfrastructure messaging)
        {
            Messaging = messaging;
        }

        /// <summary>
        /// Publishes a message to the IMessagingInfrastructure stored in Messaging. The message gets only published in case
        /// its topic matches any PublishPatterns.
        /// </summary>
        /// <param name="message">The message to publish.</param>
        internal void Publish(Message message)
        {
            if (!MessageNeedsPublication(message.Topic)) return;
            Messaging?.Publish(message);
        }

        /// <summary>
        /// Subcribes to a message topic at the IMessagingInfrastructure stored in Messaging. The subscription is only done in case
        /// the routeFilterPattern matches any SubscriptionPatterns.
        /// </summary>
        /// <param name="routeFilterPattern">The message topic pattern used for subscription.</param>
        /// <returns>A subscriptionId as object or null in case no subscription where done.</returns>
        internal MessageRoutingSubscription Subscribe<TMessageHandler>(string routeFilterPattern) where TMessageHandler : IMessageHandler
        {
            var patterns = DetermineSubscriptionPatterns(routeFilterPattern).ToArray();
            if (patterns.Length == 0) return null;

            var subscription = new MessageRoutingSubscription(Messaging);
            subscription.Subscribe<TMessageHandler>(patterns);
            return subscription;
        }

        /// <summary>
        /// Subscribes to a message topic at the IMessagingInfrastructure stored in Messaging. The subscription is only done in case
        /// the routeFilterPattern matches any SubscriptionPatterns.
        /// </summary>
        /// <param name="routeFilterPattern">The message topic pattern used for subscription.</param>
        /// <param name="handler">Action being called in case a message with matching topic arrives.</param>
        /// <returns>A subscriptionId as object or null in case no subscription where done.</returns>
        internal MessageRoutingSubscription Subscribe(string routeFilterPattern, Action<Message> handler)
        {
            var patterns = DetermineSubscriptionPatterns(routeFilterPattern).ToArray();
            if (patterns.Length == 0) return null;

            var subscription = new MessageRoutingSubscription(Messaging);
            subscription.Subscribe(patterns, handler);
            return subscription;
        }

        private bool MessageNeedsPublication(string messageTopic)
        {
            return PublishPatterns == null || PublishPatterns.Length == 0 || PublishPatterns.Any(messageTopic.IsMatch);
        }

        private IEnumerable<string> DetermineSubscriptionPatterns(string routeFilterPattern)
        {
            if (SubscriptionPatterns == null || SubscriptionPatterns.Length == 0)
            {
                yield return routeFilterPattern;
            }
            else
            {
                if (SubscriptionPatterns.Any(routeFilterPattern.IsMatch))
                    yield return routeFilterPattern;

                // in case routeFilterPattern starts with '*', probably all SubscriptionPatterns ending with '*' may match
                // (e.g. '*/rc/guid' matches 'saf/msg/*' and 'saf/reply/*' but not 'saf/topic')
                // in that case we force to return all SubscriptionPatterns ending with '*' in addition to the matching ones.
                var startsWithMultiple = routeFilterPattern.StartsWith(WildcardMatcher.Multiple.ToString());
                foreach (var pattern in SubscriptionPatterns.Where(sp => startsWithMultiple && sp.EndsWith(WildcardMatcher.Multiple.ToString()) || sp.IsMatch(routeFilterPattern)))
                    yield return pattern;
            }
        }
    }
}