// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SAF.Common;

namespace SAF.Messaging.Test
{
    internal class TestMessaging : IMessagingInfrastructure
    {
        private readonly ILogger<TestMessaging> _log;
        private readonly IServiceMessageDispatcher _messageDispatcher;
        private readonly Action<Message> _traceAction;
        
        private readonly ReaderWriterLockSlim _syncSubscriptionsByType = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly Dictionary<string, List<string>> _subscriptionsByType = new Dictionary<string, List<string>>();
        private readonly ReaderWriterLockSlim _syncSubscriptionsByLambda = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly Dictionary<string, List<Action<Message>>> _subscriptionsByLambda = new Dictionary<string, List<Action<Message>>>();

        private const string MessagingKeySeparator = "###########";

        public TestMessaging(ILogger<TestMessaging> log, IServiceMessageDispatcher messageDispatcher, Action<Message> traceAction = null)
        {
            _log = log ?? NullLogger<TestMessaging>.Instance;
            _messageDispatcher = messageDispatcher;
            _traceAction = traceAction;
        }

        public void Publish(Message message)
        {
            _log.LogDebug($"Publish to {message.Topic}");
            _traceAction?.Invoke(message);

            _syncSubscriptionsByType.EnterReadLock();
            try
            {
                foreach (var key in _subscriptionsByType.Keys)
                {
                    if (!Regex.IsMatch(message.Topic, key))
                    {
                        continue;
                    }

                    foreach (var handlerTypeName in _subscriptionsByType[key])
                    {
                        _messageDispatcher.DispatchMessage(handlerTypeName, message);
                    }
                }
            }
            finally
            {
                _syncSubscriptionsByType.ExitReadLock();
            }

            _syncSubscriptionsByLambda.EnterReadLock();
            try
            {
                foreach (var key in _subscriptionsByLambda.Keys)
                {
                    if (!Regex.IsMatch(message.Topic, key)) continue;
                    foreach (var action in _subscriptionsByLambda[key])
                    {
                        _messageDispatcher.DispatchMessage(action, message);
                    }
                }
            }
            finally
            {
                _syncSubscriptionsByLambda.ExitReadLock();
            }
        }

        public object Subscribe<TMessageHandler>() where TMessageHandler : IMessageHandler
            => Subscribe<TMessageHandler>(".*");

        public object Subscribe<TMessageHandler>(string routeFilterPattern) where TMessageHandler : IMessageHandler
        {
            var handlerType = typeof(TMessageHandler);
            _log.LogDebug($"Subscribe {handlerType} to {routeFilterPattern}");

            _syncSubscriptionsByType.EnterWriteLock();
            try
            {
                if (_subscriptionsByType.ContainsKey(routeFilterPattern))
                {
                    _subscriptionsByType[routeFilterPattern].Add(typeof(TMessageHandler).FullName);
                }
                else
                {
                    _subscriptionsByType.Add(routeFilterPattern, new List<string> {typeof(TMessageHandler).FullName});
                }
            }
            finally
            {
                _syncSubscriptionsByType.ExitWriteLock();
            }

            return $"{handlerType}{MessagingKeySeparator}{routeFilterPattern}";
        }

        public object Subscribe(Action<Message> handler) 
            => Subscribe(".*", handler);

        public object Subscribe(string routeFilterPattern, Action<Message> handler)
        {
            _log.LogDebug($"Subscribe lambda to {routeFilterPattern}");

            _syncSubscriptionsByLambda.EnterWriteLock();
            try
            {
                if (_subscriptionsByLambda.ContainsKey(routeFilterPattern))
                {
                    _subscriptionsByLambda[routeFilterPattern].Add(handler);
                }
                else
                {
                    _subscriptionsByLambda.Add(routeFilterPattern, new List<Action<Message>> {handler});
                }
            }
            finally
            {
                _syncSubscriptionsByLambda.ExitWriteLock();
            }

            return $"{handler.GetHashCode()}{MessagingKeySeparator}{routeFilterPattern}";
        }

        public void Unsubscribe(object subscription)
        {
            if(!(subscription is string subscriptionKey) || string.IsNullOrWhiteSpace(subscriptionKey))
                return;

            var kvp = subscriptionKey.Split(new[] { MessagingKeySeparator }, StringSplitOptions.RemoveEmptyEntries);

            if(kvp.Length != 2)
                return;

            var handlerType = kvp[0];
            var routeFilterPattern = kvp[1];

            _syncSubscriptionsByLambda.EnterWriteLock();
            try
            {
                if (_subscriptionsByLambda.TryGetValue(routeFilterPattern, out var handlers))
                {
                    var toBeRemoved = handlers.Where(h => $"{h.GetHashCode()}" == handlerType).ToArray();
                    foreach (var action in toBeRemoved)
                    {
                        handlers.Remove(action);
                    }

                    if (handlers.Count == 0)
                    {
                        _subscriptionsByLambda.Remove(routeFilterPattern);
                    }
                }
            }
            finally
            {
                _syncSubscriptionsByLambda.ExitWriteLock();
            }

            _syncSubscriptionsByType.EnterWriteLock();
            try
            {
                if (_subscriptionsByType.TryGetValue(routeFilterPattern, out var handlerTypes))
                {
                    handlerTypes.Remove(handlerType);

                    if (handlerTypes.Count == 0)
                    {
                        _subscriptionsByLambda.Remove(routeFilterPattern);
                    }
                }
            }
            finally
            {
                _syncSubscriptionsByType.ExitWriteLock();
            }
        }
    }
}