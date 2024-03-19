// SPDX-FileCopyrightText: 2017-2023 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;

using Microsoft.Extensions.Logging;
using Common;

public class ServiceMessageDispatcher(ILogger<ServiceMessageDispatcher> log) : IServiceMessageDispatcher
{
    private readonly Dictionary<string, Func<IMessageHandler>> _messageHandlerProviders = [];
    
    public void AddHandler<TMessageHandler>(Func<IMessageHandler> handlerFactory) where TMessageHandler : IMessageHandler
        => AddHandler(typeof(TMessageHandler), handlerFactory);

    public void AddHandler(Type handlerType, Func<IMessageHandler> handlerFactory)
        => AddHandler(handlerType.FullName!, handlerFactory);

    public void AddHandler(string handlerTypeName, Func<IMessageHandler> handlerFactory)
    {
        log.LogTrace("Add message handler {handlerTypeName}.", handlerTypeName);
        _messageHandlerProviders.Add(handlerTypeName, handlerFactory);
    }

    public void DispatchMessage<TMessageHandler>(Message message) where TMessageHandler : IMessageHandler
        => DispatchMessage(typeof(TMessageHandler).FullName!, message);

    public void DispatchMessage(Type handlerType, Message message)
        => DispatchMessage(handlerType.FullName!, message);

    public void DispatchMessage(string handlerTypeFullName, Message message)
    {
        if (!_messageHandlerProviders.TryGetValue(handlerTypeFullName, out var handlerFactory))
        {
            log.LogError("Handler {handlerTypeFullName} unknown!", handlerTypeFullName);
            return;
        }

        try
        {
            var handler = handlerFactory();

            if (!handler.CanHandle(message))
            {
                log.LogDebug("Message {messageTopic} not handled by {handlerTypeFullName}. CanHandle = false.",
                    message.Topic, handlerTypeFullName);
                return;
            }

            log.LogTrace("Dispatching message {messageTopic} with handler {handlerTypeFullName}.",
                message.Topic, handlerTypeFullName);

            handler.Handle(message);
        }
        catch (ObjectDisposedException ex)
        {
            // on system shutdown a handler, or even the handlerFactory() may throw an ObjectDisposedException, which we accept and log here.
            log.LogWarning(ex, "Object {objectName} disposed while processing message {messageTopic} with handler {handlerTypeFullName}.",
                ex.ObjectName, message.Topic, handlerTypeFullName);
        }
        catch (Exception e)
        {
            log.LogError(e, "Error while processing message {messageTopic} with handler {handlerTypeFullName}.",
                message.Topic, handlerTypeFullName);
        }
    }

    public void DispatchMessage(Action<Message> handler, Message message)
    {
        log.LogDebug("Dispatching message {messageTopic} with lambda handler of target {targetType}.",
            message.Topic, handler.Target?.ToString());
        try
        {
            handler(message);
        }
        catch (Exception e)
        {
            log.LogError(e, "Error while processing message {messageTopic} with lambda handler of target {targetType}",
                message.Topic, handler.Target?.ToString());
        }
    }
}