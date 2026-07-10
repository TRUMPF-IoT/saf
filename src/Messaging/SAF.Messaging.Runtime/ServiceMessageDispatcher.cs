// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Runtime;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SAF.Messaging.Contracts;

public class ServiceMessageDispatcher(ILogger<ServiceMessageDispatcher> log) : IServiceMessageDispatcher
{
    private readonly ConcurrentDictionary<string, Func<IMessageHandler>> _messageHandlerProvidersByRegistrationId = new();
    private readonly ConcurrentDictionary<string, string> _handlerTypeToRegistrationId = new();

    public string RegisterHandler(Func<IMessageHandler> handlerFactory, string? displayName = null)
    {
        var registrationId = Guid.NewGuid().ToString("N");
        if (!_messageHandlerProvidersByRegistrationId.TryAdd(registrationId, handlerFactory))
            throw new InvalidOperationException($"Could not register message handler factory '{displayName ?? "<unnamed>"}'.");

        log.LogTrace("Registered message handler {DisplayName} with registration id {RegistrationId}.",
            displayName ?? "<unnamed>", registrationId);

        return registrationId;
    }

    public void UnregisterHandler(string handlerRegistrationId)
    {
        _messageHandlerProvidersByRegistrationId.TryRemove(handlerRegistrationId, out _);

        foreach (var kvp in _handlerTypeToRegistrationId.Where(kvp => kvp.Value == handlerRegistrationId).ToArray())
        {
            _handlerTypeToRegistrationId.TryRemove(kvp.Key, out _);
        }

        log.LogTrace("Unregistered message handler registration {RegistrationId}.", handlerRegistrationId);
    }

    public void AddHandler<TMessageHandler>(Func<IMessageHandler> handlerFactory) where TMessageHandler : IMessageHandler
        => AddHandler(typeof(TMessageHandler), handlerFactory);

    public void AddHandler(Type handlerType, Func<IMessageHandler> handlerFactory)
        => AddHandler(handlerType.FullName!, handlerFactory);

    public void AddHandler(string handlerTypeName, Func<IMessageHandler> handlerFactory)
    {
        if (_handlerTypeToRegistrationId.ContainsKey(handlerTypeName))
            throw new ArgumentException($"Handler '{handlerTypeName}' already registered.", nameof(handlerTypeName));

        var registrationId = RegisterHandler(handlerFactory, handlerTypeName);
        if (!_handlerTypeToRegistrationId.TryAdd(handlerTypeName, registrationId))
        {
            _messageHandlerProvidersByRegistrationId.TryRemove(registrationId, out _);
            throw new ArgumentException($"Handler '{handlerTypeName}' already registered.", nameof(handlerTypeName));
        }

        log.LogTrace("Add message handler {HandlerTypeName}.", handlerTypeName);
    }

    public void DispatchMessage<TMessageHandler>(Message message) where TMessageHandler : IMessageHandler
        => DispatchMessage(typeof(TMessageHandler), message);

    public void DispatchMessage(Type handlerType, Message message)
        => DispatchMessage(handlerType.FullName!, message);

    public void DispatchMessageByRegistration(string handlerRegistrationId, Message message)
    {
        if (!_messageHandlerProvidersByRegistrationId.TryGetValue(handlerRegistrationId, out var handlerFactory))
        {
            log.LogError("Handler registration {HandlerRegistrationId} unknown!", handlerRegistrationId);
            return;
        }

        DispatchInternal(handlerFactory, message, handlerRegistrationId);
    }

    public void DispatchMessage(string handlerTypeFullName, Message message)
    {
        if (!_handlerTypeToRegistrationId.TryGetValue(handlerTypeFullName, out var registrationId))
        {
            log.LogError("Handler {HandlerTypeFullName} unknown!", handlerTypeFullName);
            return;
        }

        DispatchMessageByRegistration(registrationId, message);
    }

    private void DispatchInternal(Func<IMessageHandler> handlerFactory, Message message, string handlerDisplayName)
    {
        try
        {
            var handler = handlerFactory();

            if (!handler.CanHandle(message))
            {
                log.LogDebug("Message {MessageTopic} not handled by {HandlerDisplayName}. CanHandle = false.",
                    message.Topic, handlerDisplayName);
                return;
            }

            log.LogTrace("Dispatching message {MessageTopic} with handler {HandlerDisplayName}.",
                message.Topic, handlerDisplayName);

            handler.Handle(message);
        }
        catch (ObjectDisposedException ex)
        {
            // During shutdown, handler creation or execution may race with disposal.
            log.LogWarning(ex, "Object {ObjectName} disposed while processing message {MessageTopic} with handler {HandlerDisplayName}.",
                ex.ObjectName, message.Topic, handlerDisplayName);
        }
        catch (Exception e)
        {
            log.LogError(e, "Error while processing message {MessageTopic} with handler {HandlerDisplayName}.",
                message.Topic, handlerDisplayName);
        }
    }

    public void DispatchMessage(Action<Message> handler, Message message)
    {
        log.LogDebug("Dispatching message {MessageTopic} with lambda handler of target {TargetType}.",
            message.Topic, handler.Target?.ToString());
        try
        {
            handler(message);
        }
        catch (Exception e)
        {
            log.LogError(e, "Error while processing message {MessageTopic} with lambda handler of target {TargetType}",
                message.Topic, handler.Target?.ToString());
        }
    }
}


