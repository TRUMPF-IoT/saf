// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using SAF.Common;

namespace SAF.Messaging.Redis
{
    internal class Messaging : IRedisMessagingInfrastructure, IDisposable
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IServiceMessageDispatcher _serviceMessageDispatcher;
        private readonly Action<Message> _traceAction;
        private readonly ILogger _log;

        private readonly ConcurrentDictionary<Guid, (string routeFilterPattern, Action<RedisChannel, RedisValue> handler)> _subscriptions
            = new ConcurrentDictionary<Guid, (string routeFilterPattern, Action<RedisChannel, RedisValue> handler)>();

        public Messaging(ILogger<Messaging> log, IConnectionMultiplexer redis, IServiceMessageDispatcher serviceMessageDispatcher, Action<Message> traceAction)
        {
            _log = log ?? NullLogger<Messaging>.Instance;
            _redis = redis;
            _serviceMessageDispatcher = serviceMessageDispatcher;
            _traceAction = traceAction;
        }

        public void Publish(Message message)
        {
            _traceAction?.Invoke(message);
            try
            {
                _redis.GetSubscriber()?.Publish(message.Topic, message.Payload, CommandFlags.FireAndForget);
            }
            catch (NullReferenceException nre)
            {
                // catched in case the DI container disposed ConnectionMultiplexer in parallel
                _log.LogWarning(nre, $"Handled NullReferenceException while publishing message {message.Topic}");
            }
            catch (ObjectDisposedException ode)
            {
                // catched in case the DI container disposed ConnectionMultiplexer already
                _log.LogInformation(ode, $"Handled ObjectDisposedException while publishing message {message.Topic}");
            }
        }

        public object Subscribe<TMessageHandler>() where TMessageHandler : IMessageHandler
            => Subscribe<TMessageHandler>("*");

        public object Subscribe<TMessageHandler>(string routeFilterPattern) where TMessageHandler : IMessageHandler
        {
            _log.LogDebug($"Subscribe \"{typeof(TMessageHandler).Name}\" for route \"{routeFilterPattern}\".");

            void Handler(RedisChannel channel, RedisValue message)
            {
                try
                {
                    _serviceMessageDispatcher.DispatchMessage<TMessageHandler>(new Message
                    {
                        Topic = channel.ToString(),
                        Payload = message.ToString()
                    });
                }
                catch (Exception e) // Exceptions in redis callbacks are omitted, when not explicitly caught and logged!
                {
                    _log.LogError(e, $"Exception while trying to dispatch message \"{channel}\" from redis callback!");
                    throw;
                }
            }

            return SubscribeMessageHandler(routeFilterPattern, Handler);
        }

        public object Subscribe(Action<Message> handler) => Subscribe(".*", handler);

        public object Subscribe(string routeFilterPattern, Action<Message> handler)
        {
            _log.LogDebug($"Subscribe \"lambda handler\" for route \"{routeFilterPattern}\".");

            void Handler(RedisChannel channel, RedisValue message)
            {
                try
                {
                    _serviceMessageDispatcher.DispatchMessage(handler, new Message
                    {
                        Topic = channel.ToString(),
                        Payload = message.ToString()
                    });
                }
                catch(Exception e) // Exceptions in redis callbacks are omitted, when not explicitly caught and logged!
                {
                    _log.LogError(e, $"Exception while trying to dispatch message \"{channel}\" from redis callback!");
                    throw;
                }
            }

            return SubscribeMessageHandler(routeFilterPattern, Handler);
        }

        public void Unsubscribe(object subscription)
        {
            var subscriptionGuid = subscription as Guid?;

            if(!subscriptionGuid.HasValue)
            {
                _log.LogWarning($"Unsubscribe failed. Invalid subscription object passed: \"{subscription}\".");
                return;
            }

            if(!_subscriptions.TryRemove(subscriptionGuid.Value, out var storedSubscription))
            {
                _log.LogWarning($"Unsubscribe failed. Subscription not active anymore: \"{subscriptionGuid.Value}\".");
                return;
            }

            try
            {
                _redis.GetSubscriber()?.Unsubscribe(storedSubscription.routeFilterPattern, storedSubscription.handler);
            }
            catch (NullReferenceException nre)
            {
                // catched in case the DI container disposed ConnectionMultiplexer in parallel
                _log.LogWarning(nre, $"Handled NullReferenceException while unsubscribing pattern {storedSubscription.routeFilterPattern}");
            }
            catch (ObjectDisposedException ode)
            {
                // catched in case the DI container disposed ConnectionMultiplexer already
                _log.LogInformation(ode, $"Handled ObjectDisposedException while unsubscribing pattern {storedSubscription.routeFilterPattern}");
            }

            _log.LogDebug($"Unsubscribed subscription \"{subscriptionGuid.Value}\" for channel \"{storedSubscription.routeFilterPattern}\"");
        }

        public void Dispose()
        {
            _redis?.Dispose();
        }

        private object SubscribeMessageHandler(string routeFilterPattern, Action<RedisChannel, RedisValue> handler)
        {
            try
            {
                var subscriptionId = Guid.NewGuid();
                _redis.GetSubscriber()?.Subscribe(routeFilterPattern, handler);
                _subscriptions.TryAdd(subscriptionId, (routeFilterPattern, handler));
                return subscriptionId;
            }
            catch (NullReferenceException nre)
            {
                // catched in case the DI container disposed ConnectionMultiplexer in parallel
                _log.LogWarning(nre, $"Handled NullReferenceException while subscribing pattern {routeFilterPattern}");
            }
            catch (ObjectDisposedException ode)
            {
                // catched in case the DI container disposed ConnectionMultiplexer already
                _log.LogInformation(ode, $"Handled ObjectDisposedException while subscribing pattern {routeFilterPattern}");
            }

            return null;
        }
    }
}