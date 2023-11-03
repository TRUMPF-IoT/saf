// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using SAF.Common;
using JsonSerializer = SAF.Toolbox.Serialization.JsonSerializer;

namespace SAF.Messaging.Redis
{
    public static class RedisMessageVersion
    {
        public const string V1 = "1.0.0";
        public const string V2 = "2.0.0";
        public static readonly string Latest = V2;
    }

    internal class RedisMessage
    {
        public string? Version { get; set; }
        public Message? Message { get; set; }
    }

    internal sealed class Messaging : IRedisMessagingInfrastructure, IDisposable
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IServiceMessageDispatcher _serviceMessageDispatcher;
        private readonly Action<Message>? _traceAction;
        private readonly ILogger<Messaging> _log;

        private readonly ConcurrentDictionary<Guid, (string routeFilterPattern, Action<RedisChannel, RedisValue> handler)> _subscriptions = new();

        public Messaging(ILogger<Messaging>? log, IConnectionMultiplexer redis, IServiceMessageDispatcher serviceMessageDispatcher, Action<Message>? traceAction)
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
                var redisPayload = JsonSerializer.Serialize(new RedisMessage {Message = message, Version = RedisMessageVersion.Latest});
                _redis.GetSubscriber().Publish(RedisChannel.Literal(message.Topic), redisPayload, CommandFlags.FireAndForget);
            }
            catch (NullReferenceException nre)
            {
                // catch in case the DI container disposed ConnectionMultiplexer in parallel
                _log.LogWarning(nre, $"Handled NullReferenceException while publishing message {message.Topic}");
            }
            catch (ObjectDisposedException ode)
            {
                // catch in case the DI container disposed ConnectionMultiplexer already
                _log.LogInformation(ode, $"Handled ObjectDisposedException while publishing message {message.Topic}");
            }
        }

        public object Subscribe<TMessageHandler>() where TMessageHandler : IMessageHandler
            => Subscribe<TMessageHandler>("*");

        public object Subscribe<TMessageHandler>(string routeFilterPattern) where TMessageHandler : IMessageHandler
        {
            _log.LogDebug($"Subscribe \"{typeof(TMessageHandler).Name}\" for route \"{routeFilterPattern}\".");

            void Handler(Message message)
            {
                try
                {
                    _serviceMessageDispatcher.DispatchMessage<TMessageHandler>(message);
                }
                catch (Exception e) // Exceptions in redis callbacks are omitted, when not explicitly caught and logged!
                {
                    _log.LogError(e, $"Exception while trying to dispatch message \"{message.Topic}\" from redis callback!");
                    throw;
                }
            }

            return SubscribeMessageHandler(routeFilterPattern, Handler) ?? new object();
        }

        public object Subscribe(Action<Message> handler) => Subscribe(".*", handler);

        public object Subscribe(string routeFilterPattern, Action<Message> handler)
        {
            _log.LogDebug($"Subscribe \"lambda handler\" for route \"{routeFilterPattern}\".");

            void Handler(Message message)
            {
                try
                {
                    _serviceMessageDispatcher.DispatchMessage(handler, message);
                }
                catch(Exception e) // Exceptions in redis callbacks are omitted, when not explicitly caught and logged!
                {
                    _log.LogError(e, $"Exception while trying to dispatch message \"{message.Topic}\" from redis callback!");
                    throw;
                }
            }

            return SubscribeMessageHandler(routeFilterPattern, Handler) ?? new object();
        }

        public void Unsubscribe(object subscription)
        {
            if(subscription is not Guid subscriptionGuid)
            {
                _log.LogWarning($"Unsubscribe failed. Invalid subscription object passed: \"{subscription}\".");
                return;
            }

            if(!_subscriptions.TryRemove(subscriptionGuid, out var storedSubscription))
            {
                _log.LogWarning($"Unsubscribe failed. Subscription not active anymore: \"{subscriptionGuid}\".");
                return;
            }

            try
            {
                _redis.GetSubscriber().Unsubscribe(RedisChannel.Pattern(storedSubscription.routeFilterPattern), storedSubscription.handler);
            }
            catch (NullReferenceException nre)
            {
                // catch in case the DI container disposed ConnectionMultiplexer in parallel
                _log.LogWarning(nre, $"Handled NullReferenceException while unsubscribing pattern {storedSubscription.routeFilterPattern}");
            }
            catch (ObjectDisposedException ode)
            {
                // catch in case the DI container disposed ConnectionMultiplexer already
                _log.LogInformation(ode, $"Handled ObjectDisposedException while unsubscribing pattern {storedSubscription.routeFilterPattern}");
            }

            _log.LogDebug($"Unsubscribed subscription \"{subscriptionGuid}\" for channel \"{storedSubscription.routeFilterPattern}\"");
        }

        public void Dispose()
        {
            _redis.Dispose();
        }

        private object? SubscribeMessageHandler(string routeFilterPattern, Action<Message> handler)
        {
            try
            {
                void InternalHandler(RedisChannel channel, RedisValue message)
                {
                    RedisMessage? redisMessage;
                    try
                    {
                        redisMessage = JsonSerializer.Deserialize<RedisMessage>(message.ToString());
                        if (string.IsNullOrEmpty(redisMessage?.Version) && redisMessage?.Message == null)
                            redisMessage = null;
                    }
                    catch (Exception)
                    {
                        redisMessage = null;
                    }

                    handler(redisMessage?.Message ?? new Message
                    {
                        Topic = channel.ToString(),
                        Payload = message.ToString()
                    });
                }

                var subscriptionId = Guid.NewGuid();
                _redis.GetSubscriber().Subscribe(RedisChannel.Pattern(routeFilterPattern), InternalHandler);
                _subscriptions.TryAdd(subscriptionId, (routeFilterPattern, InternalHandler));
                return subscriptionId;
            }
            catch (NullReferenceException nre)
            {
                // catch in case the DI container disposed ConnectionMultiplexer in parallel
                _log.LogWarning(nre, $"Handled NullReferenceException while subscribing pattern {routeFilterPattern}");
            }
            catch (ObjectDisposedException ode)
            {
                // catch in case the DI container disposed ConnectionMultiplexer already
                _log.LogInformation(ode, $"Handled ObjectDisposedException while subscribing pattern {routeFilterPattern}");
            }

            return null;
        }
    }
}