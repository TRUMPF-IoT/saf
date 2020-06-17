// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿
using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SAF.Common;
using SAF.Communication.PubSub.Interfaces;

namespace SAF.Messaging.Cde
{
    internal class Messaging : ICdeMessagingInfrastructure
    {
        private readonly ILogger _log;
        private readonly IServiceMessageDispatcher _dispatcher;
        private readonly IPublisher _publisher;
        private readonly ISubscriber _subscriber;
        private readonly Action<Message> _traceAction;
        private readonly CdeMessagingConfiguration _config;

        private readonly ConcurrentDictionary<string, (string pattern, ISubscription subscription)> _subscriptions = new ConcurrentDictionary<string, (string pattern, ISubscription subscription)>();

        public Messaging(ILogger<Messaging> log, IServiceMessageDispatcher dispatcher, IPublisher publisher, ISubscriber subscriber, Action<Message> traceAction)
            : this(log, dispatcher, publisher, subscriber, traceAction, null)
        { }

        public Messaging(ILogger<Messaging> log, IServiceMessageDispatcher dispatcher, IPublisher publisher, ISubscriber subscriber, Action<Message> traceAction, CdeMessagingConfiguration config)
        {
            _log = log as ILogger ?? NullLogger.Instance;
            _dispatcher = dispatcher;

            _publisher = publisher;
            _subscriber = subscriber;

            _traceAction = traceAction;

            _config = config ?? new CdeMessagingConfiguration();
        }

        public void Publish(Message message)
        {
            _log.LogDebug($"Publish message \"{message.Topic}\" with RelayOptions={_config.RoutingOptions}.");
            _traceAction?.Invoke(message);

            _publisher?.Publish(message.Topic, message.Payload, _config.RoutingOptions);
        }

        public object Subscribe<TMessageHandler>() where TMessageHandler : IMessageHandler => Subscribe<TMessageHandler>("*");

        public object Subscribe<TMessageHandler>(string routeFilterPattern) where TMessageHandler : IMessageHandler
        {
            _log.LogDebug($"Subscribe \"{typeof(TMessageHandler).Name}\" for route \"{routeFilterPattern}\", RelayOptions={_config.RoutingOptions}.");

            return Subscribe(routeFilterPattern, (channel, message) =>
            {
                try
                {
                    _dispatcher.DispatchMessage<TMessageHandler>(new Message
                    {
                        Topic = channel,
                        Payload = message
                    });
                }
                catch (Exception e) // Exceptions in CDE callbacks are omitted, when not explicitly caught and logged!
                {
                    _log.LogError(e, $"Exception while trying to dispatch message \"{channel}\" from CDE callback!");
                    throw;
                }
            });
        }

        public object Subscribe(Action<Message> handler) => Subscribe("*", handler);

        public object Subscribe(string routeFilterPattern, Action<Message> handler)
        {
            _log.LogDebug($"Subscribe \"lambda handler\" for route \"{routeFilterPattern}\", RelayOptions={_config.RoutingOptions}.");

            return Subscribe(routeFilterPattern, (channel, message) =>
            {
                try
                {
                    _dispatcher.DispatchMessage(handler, new Message
                    {
                        Topic = channel,
                        Payload = message
                    });
                }
                catch (Exception e) // Exceptions in CDE callbacks are omitted, when not explicitly caught and logged!
                {
                    _log.LogError(e, $"Exception while trying to dispatch message \"{channel}\" from CDE callback!");
                    throw;
                }
            });
        }

        public void Unsubscribe(object subscription)
        {
            var subscriptionId = subscription as string;
            if(string.IsNullOrEmpty(subscriptionId))
            {
                _log.LogWarning($"Unsubscribe failed. Invalid subscription object passed: \"{subscription}\".");
                return;
            }

            if(!_subscriptions.TryRemove(subscriptionId, out var sub))
            {
                _log.LogWarning($"Unsubscribe failed. Subscription not active anymore: \"{subscriptionId}\".");
                return;
            }

            sub.subscription.Unsubscribe();
            _log.LogDebug($"Unsubscribed subscription \"{subscriptionId}\" for channel \"{sub.pattern}\"");
        }

        private string Subscribe(string pattern, Action<string, string> handler)
        {
            var subscriptionId = Guid.NewGuid().ToString();

            var subscription = _subscriber.Subscribe(_config.RoutingOptions, pattern);
            subscription.With((time, channel, message) => handler(channel, message));
            if (!_subscriptions.TryAdd(subscriptionId, (pattern, subscription)))
                throw new ArgumentException("An element with the same key already exists!");

            return subscriptionId;
        }
    }
}