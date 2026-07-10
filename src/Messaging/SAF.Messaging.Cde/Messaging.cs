// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0


namespace SAF.Messaging.Cde;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SAF.Common;
using SAF.Messaging.Contracts;
using Communication.PubSub.Interfaces;

/// <summary>
/// Coordinates the publishing and receiving of messages via the C-DEngine.
/// </summary>
internal class Messaging : ICdeMessagingInfrastructure
{
    private readonly ILogger<Messaging> _log;
    private readonly Func<Type, IMessageHandler>? _handlerFactory;
    private readonly IServiceMessageDispatcher _dispatcher;
    private readonly IPublisher _publisher;
    private readonly ISubscriber _subscriber;
    private readonly Action<Message>? _traceAction;
    private readonly CdeMessagingConfiguration _config;

    private readonly ConcurrentDictionary<string, (string pattern, ISubscription subscription)> _subscriptions = new();
    private readonly ConcurrentDictionary<string, string> _typedHandlerRegistrationBySubscriptionId = new();

    public Messaging(ILogger<Messaging>? log, IServiceMessageDispatcher dispatcher, IPublisher publisher, ISubscriber subscriber, Action<Message>? traceAction, Func<Type, IMessageHandler>? handlerFactory = null)
        : this(log, dispatcher, publisher, subscriber, traceAction, new CdeMessagingConfiguration(), handlerFactory)
    { }

    public Messaging(ILogger<Messaging>? log, IServiceMessageDispatcher dispatcher, IPublisher publisher, ISubscriber subscriber, Action<Message>? traceAction, CdeMessagingConfiguration config, Func<Type, IMessageHandler>? handlerFactory = null)
    {
        _log = log ?? NullLogger<Messaging>.Instance;
        _handlerFactory = handlerFactory;
        _dispatcher = dispatcher;

        _publisher = publisher;
        _subscriber = subscriber;

        _traceAction = traceAction;

        _config = config;
    }

    public void Publish(Message message)
    {
        _log.LogDebug($"Publish message \"{message.Topic}\" with RelayOptions={_config.RoutingOptions}.");
        _traceAction?.Invoke(message);

        _publisher.Publish(message, _config.RoutingOptions);
    }

    public object Subscribe<TMessageHandler>() where TMessageHandler : IMessageHandler => Subscribe<TMessageHandler>("*");

    public object Subscribe<TMessageHandler>(string routeFilterPattern) where TMessageHandler : IMessageHandler
    {
        _log.LogDebug($"Subscribe \"{typeof(TMessageHandler).Name}\" for route \"{routeFilterPattern}\", RelayOptions={_config.RoutingOptions}.");

        var handlerTypeName = typeof(TMessageHandler).AssemblyQualifiedName ?? typeof(TMessageHandler).FullName ?? typeof(TMessageHandler).Name;
        var handlerRegistrationId = _handlerFactory != null
            ? _dispatcher.RegisterHandler(() => _handlerFactory(typeof(TMessageHandler)), handlerTypeName)
            : null;

        var subscriptionId = InternalSubscribe(routeFilterPattern, message =>
        {
            try
            {
                if (handlerRegistrationId != null)
                {
                    _dispatcher.DispatchMessageByRegistration(handlerRegistrationId, message);
                }
                else
                {
                    _dispatcher.DispatchMessage<TMessageHandler>(message);
                }
            }
            catch (Exception e) // Exceptions in CDE callbacks are omitted, when not explicitly caught and logged!
            {
                _log.LogError(e, $"Exception while trying to dispatch message \"{message.Topic}\" from CDE callback!");
                throw;
            }
        });

        if (handlerRegistrationId != null)
            _typedHandlerRegistrationBySubscriptionId.TryAdd(subscriptionId, handlerRegistrationId);

        return subscriptionId;
    }

    public object Subscribe(Action<Message> handler) => Subscribe("*", handler);

    public object Subscribe(string routeFilterPattern, Action<Message> handler)
    {
        _log.LogDebug("Subscribe lambda handler of type {targetType} for route {routeFilterPattern}, RelayOptions={routingOptions}.",
            handler.Target?.ToString(), routeFilterPattern, _config.RoutingOptions);

        return InternalSubscribe(routeFilterPattern, message =>
        {
            try
            {
                _dispatcher.DispatchMessage(handler, message);
            }
            catch (Exception e) // Exceptions in CDE callbacks are omitted, when not explicitly caught and logged!
            {
                _log.LogError(e, $"Exception while trying to dispatch message \"{message.Topic}\" from CDE callback!");
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

        if (_typedHandlerRegistrationBySubscriptionId.TryRemove(subscriptionId, out var handlerRegistrationId))
        {
            _dispatcher.UnregisterHandler(handlerRegistrationId);
        }

        sub.subscription.Unsubscribe();
        _log.LogDebug($"Unsubscribed subscription \"{subscriptionId}\" for channel \"{sub.pattern}\"");
    }

    private string InternalSubscribe(string pattern, Action<Message> handler)
    {
        var subscription = _subscriber.Subscribe(_config.RoutingOptions, pattern);
        var subscriptionId = subscription.Id.ToString();
        subscription.SetHandler((_, message) => handler(message));
        if (!_subscriptions.TryAdd(subscriptionId, (pattern, subscription)))
            throw new ArgumentException("An element with the same key already exists!");

        return subscriptionId;
    }
}


