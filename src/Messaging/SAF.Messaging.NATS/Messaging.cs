using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using SAF.Common;

namespace SAF.Messaging.Nats;

public class Messaging : INatsMessagingInfrastructure, IDisposable
{
    private readonly INatsClient _natsClient;
    private readonly IServiceMessageDispatcher _serviceMessageDispatcher;
    private readonly Action<Message>? _traceAction;
    private readonly ILogger<Messaging> _logger;

    private readonly ConcurrentDictionary<Guid, (string routeFilterPattern, CancellationTokenSource cancellationTokenSource, Task)> _subscriptions = new();

    public Messaging(ILogger<Messaging>? logger, INatsClient natsClient,
        IServiceMessageDispatcher serviceMessageDispatcher, Action<Message>? traceAction)
    {
        _logger = logger ?? NullLogger<Messaging>.Instance;
        _natsClient = natsClient;
        _serviceMessageDispatcher = serviceMessageDispatcher;
        _traceAction = traceAction;
    }

    public void Publish(Message message)
    {
        _traceAction?.Invoke(message);

        try
        {
            _natsClient.PublishAsync(message.Topic, message.Payload);
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

        void Handler(Message message)
        {
            try
            {
                _serviceMessageDispatcher.DispatchMessage<TMessageHandler>(message);
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

        if(!_subscriptions.TryRemove(subscriptionGuid, out var storedSubscription))
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
            var cts = new CancellationTokenSource();
            var subscriptionTask = Task.Run(async () =>
            {
                await foreach (var msg in _natsClient.SubscribeAsync<string>(subject: routeFilterPattern, cancellationToken: cts.Token))
                {
                    try
                    {
                        var message = new Message
                        {
                            Topic = msg.Subject,
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

            var subscriptionId = Guid.NewGuid();
            _subscriptions.TryAdd(subscriptionId, (routeFilterPattern, cts, subscriptionTask));

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
