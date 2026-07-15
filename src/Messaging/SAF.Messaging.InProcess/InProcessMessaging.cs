// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.InProcess;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SAF.Common;
using SAF.Messaging.Contracts;

internal class InProcessMessaging : IMessagingInfrastructure, IDisposable
{
    private sealed record TypedSubscription(Type HandlerType, string RouteFilterPattern);

    private readonly ILogger<InProcessMessaging> _log;
    private readonly IServiceMessageDispatcher _messageDispatcher;

    private readonly ReaderWriterLockSlim _syncSubscriptionsByType = new(LockRecursionPolicy.SupportsRecursion);
    private readonly Dictionary<string, List<Type>> _subscriptionsByType = new();

    private readonly ReaderWriterLockSlim _syncSubscriptionsByLambda = new(LockRecursionPolicy.SupportsRecursion);
    private readonly Dictionary<string, List<Action<Message>>> _subscriptionsByLambda = new();
    private const string MessagingKeySeparator = "###########";

    public InProcessMessaging(ILogger<InProcessMessaging>? log, IServiceMessageDispatcher messageDispatcher)
    {
        _log = log ?? NullLogger<InProcessMessaging>.Instance;
        _messageDispatcher = messageDispatcher;
    }
        
    public object Subscribe<TMessageHandler>() where TMessageHandler : IMessageHandler
        => Subscribe<TMessageHandler>("*");

    public object Subscribe(Action<Message> handler)
        => Subscribe("*", handler);
        
    public object Subscribe<TMessageHandler>(string routeFilterPattern) where TMessageHandler : IMessageHandler
    {
        var handlerType = typeof(TMessageHandler);
        _log.LogDebug($"Subscribe {handlerType} to {routeFilterPattern}");

        _syncSubscriptionsByType.EnterWriteLock();
        try
        {
            if (_subscriptionsByType.TryGetValue(routeFilterPattern, out var handlerList))
            {
                handlerList.Add(handlerType);
            }
            else
            {
                _subscriptionsByType.Add(routeFilterPattern, new List<Type> { handlerType });
            }
        }
        finally
        {
            _syncSubscriptionsByType.ExitWriteLock();
        }

        return new TypedSubscription(handlerType, routeFilterPattern);
    }

    public object Subscribe(string routeFilterPattern, Action<Message> handler)
    {
        _log.LogDebug($"Subscribe lambda to {routeFilterPattern}");

        _syncSubscriptionsByLambda.EnterWriteLock();
        try
        {
            if (_subscriptionsByLambda.TryGetValue(routeFilterPattern, out var lambdaList))
            {
                lambdaList.Add(handler);
            }
            else
            {
                _subscriptionsByLambda.Add(routeFilterPattern, new List<Action<Message>> { handler });
            }
        }
        finally
        {
            _syncSubscriptionsByLambda.ExitWriteLock();
        }

        return $"{handler.GetHashCode()}{MessagingKeySeparator}{routeFilterPattern}";
    }

    public void Publish(Message message)
    {
        _log.LogDebug($"Publish to {message.Topic}");
        _log.LogTrace("Publishing message for topic {Topic}.", message.Topic);

        var subscriptionsToRun = new List<Func<Task>>();

        _syncSubscriptionsByType.EnterReadLock();
        try
        {
            foreach (var kvp in _subscriptionsByType)
            {
                if (!message.Topic.IsMatch(kvp.Key))
                    continue;

                foreach (var handlerType in kvp.Value)
                    subscriptionsToRun.Add(PrepareTaskWithErrorHandler(handlerType.ToString(), () => _messageDispatcher.DispatchMessage(handlerType, message)));
            }
        }
        finally
        {
            _syncSubscriptionsByType.ExitReadLock();
        }

        _syncSubscriptionsByLambda.EnterReadLock();
        try
        {
            foreach (var subscriptionTopic in _subscriptionsByLambda.Select(kvp => kvp.Key))
            {
                if (!message.Topic.IsMatch(subscriptionTopic))
                    continue;

                foreach (var action in _subscriptionsByLambda[subscriptionTopic])
                    subscriptionsToRun.Add(PrepareTaskWithErrorHandler("<Lambda>", () => _messageDispatcher.DispatchMessage(action, message)));
            }
        }
        finally
        {
            _syncSubscriptionsByLambda.ExitReadLock();
        }

        _ = Task.WhenAll(subscriptionsToRun.Select(t => t()))
            .ContinueWith(_ => _log.LogTrace("Finished invoking {subscriptionCount} handlers.", subscriptionsToRun.Count));
    }

    public void Unsubscribe(object subscription)
    {
        if (subscription is TypedSubscription typedSubscription)
        {
            var handlerType = typedSubscription.HandlerType;
            var routeFilterPattern = typedSubscription.RouteFilterPattern;

            _syncSubscriptionsByType.EnterWriteLock();
            try
            {
                if (_subscriptionsByType.TryGetValue(routeFilterPattern, out var handlerTypes))
                {
                    handlerTypes.Remove(handlerType);

                    if (handlerTypes.Count == 0)
                    {
                        _subscriptionsByType.Remove(routeFilterPattern);
                    }
                }
            }
            finally
            {
                _syncSubscriptionsByType.ExitWriteLock();
            }

            return;
        }

        if (subscription is not string subscriptionKey || string.IsNullOrWhiteSpace(subscriptionKey))
            return;

        var kvp = subscriptionKey.Split(new[] { MessagingKeySeparator }, StringSplitOptions.RemoveEmptyEntries);

        if (kvp.Length != 2)
            return;

        var handlerHashCode = kvp[0];
        var lambdaRouteFilterPattern = kvp[1];

        _syncSubscriptionsByLambda.EnterWriteLock();
        try
        {
            if (_subscriptionsByLambda.TryGetValue(lambdaRouteFilterPattern, out var handlers))
            {
                var toBeRemoved = handlers.Where(h => $"{h.GetHashCode()}" == handlerHashCode).ToArray();
                foreach (var action in toBeRemoved)
                {
                    handlers.Remove(action);
                }

                if (handlers.Count == 0)
                {
                    _subscriptionsByLambda.Remove(lambdaRouteFilterPattern);
                }
            }
        }
        finally
        {
            _syncSubscriptionsByLambda.ExitWriteLock();
        }
    }

    private Func<Task> PrepareTaskWithErrorHandler(string handlerName, Action action)
        => () => Task.Run(action).ContinueWith(t =>
        {
            _log.LogError(t.Exception, "Error while executing subscription handler {0}.", handlerName);

            if (Debugger.IsAttached)
                Debugger.Break();

        }, TaskContinuationOptions.NotOnRanToCompletion);

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        _syncSubscriptionsByLambda.EnterWriteLock();
        try
        {
            _subscriptionsByLambda.Clear();
        }
        finally
        {
            _syncSubscriptionsByLambda.ExitWriteLock();
        }

        _syncSubscriptionsByType.EnterWriteLock();
        try
        {
            _subscriptionsByType.Clear();
        }
        finally
        {
            _syncSubscriptionsByType.ExitWriteLock();
        }

        _syncSubscriptionsByLambda.Dispose();
        _syncSubscriptionsByType.Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}


