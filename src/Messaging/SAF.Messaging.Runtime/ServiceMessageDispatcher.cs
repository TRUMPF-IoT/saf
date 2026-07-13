// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Runtime;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SAF.Messaging.Contracts;

public class ServiceMessageDispatcher : IServiceMessageDispatcher
{
    private readonly ILogger<ServiceMessageDispatcher> _log;
    private readonly IReadOnlyList<IMessageHandlerResolver> _messageHandlerResolvers;

    private readonly ConcurrentDictionary<Type, IMessageHandlerResolver> _resolverCacheByHandlerType = new();
    private readonly ConcurrentDictionary<Type, byte> _negativeResolverCacheByHandlerType = new();

    public ServiceMessageDispatcher(ILogger<ServiceMessageDispatcher> log, IEnumerable<IMessageHandlerResolver> messageHandlerResolvers)
    {
        _log = log;
        _messageHandlerResolvers = messageHandlerResolvers.ToList();
    }

    public void DispatchMessage<TMessageHandler>(Message message) where TMessageHandler : IMessageHandler
        => DispatchMessage(typeof(TMessageHandler), message);

    public void DispatchMessage(Type handlerType, Message message)
    {
        if (_negativeResolverCacheByHandlerType.ContainsKey(handlerType))
        {
            _log.LogError("Handler {HandlerType} unknown!", handlerType);
            return;
        }

        if (!_resolverCacheByHandlerType.TryGetValue(handlerType, out var cachedResolver))
        {
            if (!TryResolveHandler(handlerType, out cachedResolver, out var handler))
                return;

            _resolverCacheByHandlerType.TryAdd(handlerType, cachedResolver);
            DispatchInternal(() => handler, message, handlerType.Name);
            return;
        }

        try
        {
            DispatchInternal(() => cachedResolver.Resolve(handlerType), message, handlerType.Name);
        }
        catch (Exception ex)
        {
            // Resolver may become invalid during shutdown; evict and let next dispatch retry resolver discovery.
            _resolverCacheByHandlerType.TryRemove(handlerType, out _);
            _log.LogError(ex, "Error while resolving handler {HandlerType} via resolver cache.", handlerType);
        }
    }

    public void DispatchMessage(Action<Message> handler, Message message)
    {
        _log.LogDebug("Dispatching message {MessageTopic} with lambda handler of target {TargetType}.",
            message.Topic, handler.Target?.ToString());
        try
        {
            handler(message);
        } catch (Exception e)
        {
            _log.LogError(e, "Error while processing message {MessageTopic} with lambda handler of target {TargetType}",
                message.Topic, handler.Target?.ToString());
        }
    }

    private bool TryResolveHandler(Type handlerType, out IMessageHandlerResolver resolver, out IMessageHandler handler)
    {
        foreach (var candidateResolver in _messageHandlerResolvers)
        {
            try
            {
                handler = candidateResolver.Resolve(handlerType);
                resolver = candidateResolver;
                return true;
            }
            catch (InvalidOperationException)
            {
                // This resolver does not own the handler type.
            }
        }

        resolver = default!;
        handler = default!;
        _negativeResolverCacheByHandlerType.TryAdd(handlerType, 0);
        _log.LogError("Handler {HandlerType} unknown!", handlerType);
        return false;
    }

    private void DispatchInternal(Func<IMessageHandler> handlerFactory, Message message, string handlerDisplayName)
    {
        try
        {
            var handler = handlerFactory();

            if (!handler.CanHandle(message))
            {
                _log.LogDebug("Message {MessageTopic} not handled by {HandlerDisplayName}. CanHandle = false.",
                    message.Topic, handlerDisplayName);
                return;
            }

            _log.LogTrace("Dispatching message {MessageTopic} with handler {HandlerDisplayName}.",
                message.Topic, handlerDisplayName);

            handler.Handle(message);
        }
        catch (ObjectDisposedException ex)
        {
            // During shutdown, handler creation or execution may race with disposal.
            _log.LogWarning(ex, "Object {ObjectName} disposed while processing message {MessageTopic} with handler {HandlerDisplayName}.",
                ex.ObjectName, message.Topic, handlerDisplayName);
        }
        catch (Exception e)
        {
            _log.LogError(e, "Error while processing message {MessageTopic} with handler {HandlerDisplayName}.",
                message.Topic, handlerDisplayName);
        }
    }
}