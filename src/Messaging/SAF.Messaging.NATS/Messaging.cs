// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;
using System.Collections.Concurrent;
using SAF.Common;
using SAF.Messaging.Contracts;

namespace SAF.Messaging.Nats;

internal sealed class Messaging : INatsMessagingInfrastructure, IDisposable
{
    private readonly INatsClient _natsClient;
    private readonly IServiceProvider? _serviceProvider;
    private readonly INatsSubscriptionManager _subscriptionManager;
    private readonly IInputRouteTranslator _inputRouteTranslator;
    private readonly IOutputRouteTranslator _outputRouteTranslator;
    private readonly IServiceMessageDispatcher _serviceMessageDispatcher;
    private readonly Action<Message>? _traceAction;
    private readonly ILogger<Messaging> _logger;
    private readonly ConcurrentDictionary<Guid, string> _typedHandlerRegistrationBySubscriptionId = new();

    public Messaging(ILogger<Messaging>? logger, INatsClient natsClient,
        INatsSubscriptionManager subscriptionManager,
        IInputRouteTranslator inputRouteTranslator,
        IOutputRouteTranslator outputRouteTranslator,
        IServiceMessageDispatcher serviceMessageDispatcher, Action<Message>? traceAction, IServiceProvider? serviceProvider = null)
    {
        _logger = logger ?? NullLogger<Messaging>.Instance;
        _natsClient = natsClient;
        _serviceProvider = serviceProvider;
        _subscriptionManager = subscriptionManager;
        _inputRouteTranslator = inputRouteTranslator;
        _outputRouteTranslator = outputRouteTranslator;
        _serviceMessageDispatcher = serviceMessageDispatcher;
        _traceAction = traceAction;
    }

    public void Publish(Message message)
    {
        _traceAction?.Invoke(message);

        try
        {
            var topic = _inputRouteTranslator.TranslateRoute(message.Topic);
            _natsClient.PublishAsync(topic, message.Payload);
        }
        catch (NullReferenceException nre)
        {
            // catch in case the DI container disposed in parallel
            _logger.LogWarning(nre, $"Handled NullReferenceException while publishing message {message.Topic}");
        }
        catch (ObjectDisposedException ode)
        {
            // catch in case the DI container disposed already
            _logger.LogInformation(ode, $"Handled ObjectDisposedException while publishing message {message.Topic}");
        }
    }

    public object Subscribe<TMessageHandler>() where TMessageHandler : IMessageHandler
        => Subscribe<TMessageHandler>(">");

    public object Subscribe<TMessageHandler>(string routeFilterPattern) where TMessageHandler : IMessageHandler
    {
        _logger.LogDebug($"Subscribe \"{typeof(TMessageHandler).Name}\" for route \"{routeFilterPattern}\".");

        var handlerTypeName = typeof(TMessageHandler).AssemblyQualifiedName ?? typeof(TMessageHandler).FullName ?? typeof(TMessageHandler).Name;
        var handlerRegistrationId = _serviceProvider != null
            ? _serviceMessageDispatcher.RegisterHandler(() => (IMessageHandler)_serviceProvider.GetRequiredService<TMessageHandler>(), handlerTypeName)
            : null;

        void Handler(Message message)
        {
            try
            {
                if (handlerRegistrationId != null)
                {
                    _serviceMessageDispatcher.DispatchMessageByRegistration(handlerRegistrationId, message);
                }
                else
                {
                    _serviceMessageDispatcher.DispatchMessage<TMessageHandler>(message);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e,
                    $"Exception while trying to dispatch message \"{message.Topic}\" from redis callback!");
                throw;
            }
        }

        var subscription = SubscribeMessageHandler(routeFilterPattern, Handler);
        if (subscription is Guid subscriptionId)
        {
            if (handlerRegistrationId != null)
                _typedHandlerRegistrationBySubscriptionId.TryAdd(subscriptionId, handlerRegistrationId);

            return subscriptionId;
        }

        if (handlerRegistrationId != null)
            _serviceMessageDispatcher.UnregisterHandler(handlerRegistrationId);

        return new object();
    }

    public object Subscribe(Action<Message> handler)
        => Subscribe(">", handler);

    public object Subscribe(string routeFilterPattern, Action<Message> handler)
    {
        _logger.LogDebug($"Subscribe \"lambda handler\" for route \"{routeFilterPattern}\".");

        void Handler(Message message)
        {
            try
            {
                _serviceMessageDispatcher.DispatchMessage(handler, message);
            }
            catch (Exception e)
            {
                _logger.LogError(e,
                    $"Exception while trying to dispatch message \"{message.Topic}\" from redis callback!");
                throw;
            }
        }

        return SubscribeMessageHandler(routeFilterPattern, Handler) ?? new object();
    }

    public void Unsubscribe(object subscription)
    {
        if(subscription is not Guid subscriptionGuid)
        {
            _logger.LogWarning($"Unsubscribe failed. Invalid subscription object passed: \"{subscription}\".");
            return;
        }

        if (_typedHandlerRegistrationBySubscriptionId.TryRemove(subscriptionGuid, out var handlerRegistrationId))
        {
            _serviceMessageDispatcher.UnregisterHandler(handlerRegistrationId);
        }

        if(!_subscriptionManager.TryRemove(subscriptionGuid, out var storedSubscription))
        {
            _logger.LogWarning($"Unsubscribe failed. Subscription not active anymore: \"{subscriptionGuid}\".");
            return;
        }

        try
        {
            storedSubscription.cancellationTokenSource.Cancel();
        }
        catch (NullReferenceException nre)
        {
            // catch in case the DI container disposed in parallel
            _logger.LogWarning(nre, $"Handled NullReferenceException while unsubscribing pattern {storedSubscription.routeFilterPattern}");
        }
        catch (ObjectDisposedException ode)
        {
            // catch in case the DI container disposed already
            _logger.LogInformation(ode, $"Handled ObjectDisposedException while unsubscribing pattern {storedSubscription.routeFilterPattern}");
        }

        _logger.LogDebug($"Unsubscribed subscription \"{subscriptionGuid}\" for channel \"{storedSubscription.routeFilterPattern}\"");
    }

    public void Dispose()
    {
        _natsClient.DisposeAsync().GetAwaiter().GetResult();
    }

    private object? SubscribeMessageHandler(string routeFilterPattern, Action<Message> handler)
    {
        try
        {
            var isSynchronized = false;
            var subject = _inputRouteTranslator.TranslateRoute(routeFilterPattern);
            var cts = new CancellationTokenSource();
            var subscriptionTask = Task.Run(async () =>
            {
                var subscription = await _natsClient.Connection.SubscribeCoreAsync<string>(subject: subject, cancellationToken: cts.Token);
                isSynchronized = true;
                await foreach (var msg in subscription.Msgs.ReadAllAsync(cts.Token))
                {
                    try
                    {
                        var message = new Message
                        {
                            Topic = _outputRouteTranslator.TranslateRoute(msg.Subject),
                            Payload = msg.Data
                        };

                        handler(message);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
            }, cts.Token);

            WaitUtilities.WaitUntil(() => isSynchronized, cts.Token).Wait(cts.Token);

            var subscriptionId = Guid.NewGuid();
            _subscriptionManager.TryAdd(subscriptionId, (routeFilterPattern, cts, subscriptionTask));

            return subscriptionId;
        }
        catch (NullReferenceException nre)
        {
            // catch in case the DI container disposed in parallel
            _logger.LogWarning(nre, $"Handled NullReferenceException while subscribing pattern {routeFilterPattern}");
        }
        catch (ObjectDisposedException ode)
        {
            // catch in case the DI container disposed already
            _logger.LogInformation(ode, $"Handled ObjectDisposedException while subscribing pattern {routeFilterPattern}");
        }

        return null;
    }
}


