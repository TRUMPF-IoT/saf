// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using SAF.Common;
using SAF.Messaging.Contracts;

namespace SAF.Messaging.Nats;

internal sealed class Messaging : IMessagingInfrastructure, IDisposable
{
    private readonly INatsClient _natsClient;
    private readonly INatsSubscriptionManager _subscriptionManager;
    private readonly IInputRouteTranslator _inputRouteTranslator;
    private readonly IOutputRouteTranslator _outputRouteTranslator;
    private readonly IServiceMessageDispatcher _serviceMessageDispatcher;
    private readonly Action<Message>? _traceAction;
    private readonly ILogger<Messaging> _logger;

    public Messaging(ILogger<Messaging>? logger, INatsClient natsClient,
        INatsSubscriptionManager subscriptionManager,
        IInputRouteTranslator inputRouteTranslator,
        IOutputRouteTranslator outputRouteTranslator,
        IServiceMessageDispatcher serviceMessageDispatcher, Action<Message>? traceAction)
    {
        _logger = logger ?? NullLogger<Messaging>.Instance;
        _natsClient = natsClient;
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
            _logger.LogWarning(nre, "Handled NullReferenceException while publishing message {Topic}", message.Topic);
        }
        catch (ObjectDisposedException ode)
        {
            // catch in case the DI container disposed already
            _logger.LogInformation(ode, "Handled ObjectDisposedException while publishing message {Topic}", message.Topic);
        }
    }

    public object Subscribe<TMessageHandler>() where TMessageHandler : IMessageHandler
        => Subscribe<TMessageHandler>(">");

    public object Subscribe<TMessageHandler>(string routeFilterPattern) where TMessageHandler : IMessageHandler
    {
        _logger.LogDebug("Subscribe {HandlerName} for route {RoutePattern}.", typeof(TMessageHandler).Name, routeFilterPattern);

        void Handler(Message message)
        {
            try
            {
                _serviceMessageDispatcher.DispatchMessage<TMessageHandler>(message);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception while trying to dispatch message {Topic} from NATS callback!", message.Topic);
                throw;
            }
        }

        var subscription = SubscribeMessageHandler(routeFilterPattern, Handler);
        if (subscription is Guid subscriptionId)
        {
            return subscriptionId;
        }

        return new object();
    }

    public object Subscribe(Action<Message> handler)
        => Subscribe(">", handler);

    public object Subscribe(string routeFilterPattern, Action<Message> handler)
    {
        _logger.LogDebug("Subscribe lambda handler for route {RoutePattern}.", routeFilterPattern);

        void Handler(Message message)
        {
            try
            {
                _serviceMessageDispatcher.DispatchMessage(handler, message);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception while trying to dispatch message {Topic} from NATS callback!", message.Topic);
                throw;
            }
        }

        return SubscribeMessageHandler(routeFilterPattern, Handler) ?? new object();
    }

    public void Unsubscribe(object subscription)
    {
        if(subscription is not Guid subscriptionGuid)
        {
            _logger.LogWarning("Unsubscribe failed. Invalid subscription object passed: {Subscription}.", subscription);
            return;
        }

        if(!_subscriptionManager.TryRemove(subscriptionGuid, out var storedSubscription))
        {
            _logger.LogWarning("Unsubscribe failed. Subscription not active anymore: {SubscriptionGuid}.", subscriptionGuid);
            return;
        }

        try
        {
            storedSubscription.cancellationTokenSource.Cancel();
        }
        catch (NullReferenceException nre)
        {
            // catch in case the DI container disposed in parallel
            _logger.LogWarning(nre, "Handled NullReferenceException while unsubscribing pattern {RoutePattern}", storedSubscription.routeFilterPattern);
        }
        catch (ObjectDisposedException ode)
        {
            // catch in case the DI container disposed already
            _logger.LogInformation(ode, "Handled ObjectDisposedException while unsubscribing pattern {RoutePattern}", storedSubscription.routeFilterPattern);
        }

        _logger.LogDebug("Unsubscribed subscription {SubscriptionId} for channel {RoutePattern}", subscriptionGuid, storedSubscription.routeFilterPattern);
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
            _logger.LogWarning(nre, "Handled NullReferenceException while subscribing pattern {RoutePattern}", routeFilterPattern);
        }
        catch (ObjectDisposedException ode)
        {
            // catch in case the DI container disposed already
            _logger.LogInformation(ode, "Handled ObjectDisposedException while subscribing pattern {RoutePattern}", routeFilterPattern);
        }

        return null;
    }
}


