// SPDX-FileCopyrightText: 2017-2023 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SAF.Common;

namespace SAF.Hosting
{
    public class ServiceMessageDispatcher : IServiceMessageDispatcher
    {
        private readonly Dictionary<string, Func<IMessageHandler>> _messageHandlerProviders = new();
        private readonly ILogger<ServiceMessageDispatcher> _log;

        public ServiceMessageDispatcher(ILogger<ServiceMessageDispatcher>? log)
        {
            _log = log ?? NullLogger<ServiceMessageDispatcher>.Instance;
        }

        public void DispatchMessage(string handlerTypeFullName, Message message)
        {
            if(!_messageHandlerProviders.ContainsKey(handlerTypeFullName))
            {
                _log.LogError($"Handler {handlerTypeFullName} unknown!");
                return;
            }

            try
            {
                var provider = _messageHandlerProviders[handlerTypeFullName];
                var handler = provider();

                if (handler.CanHandle(message))
                {
                    _log.LogTrace($"Dispatching message {message.Topic} with handler {handlerTypeFullName}.");

                    try
                    {
                        handler.Handle(message);
                    }
                    catch (FileNotFoundException fnfE)
                    {
                        _log.LogError(fnfE, $"File not found error while processing message  {message.Topic} with handler {handlerTypeFullName}. " +
                                            "Is the \"CopyLocalLockFileAssemblies\" property in your Microservice project set? " +
                                            "See docs/service-devguide/SetupMicroserviceProject.md for more information.");
                    }
                    catch (Exception e)
                    {
                        _log.LogError(e, $"Error while processing message {message.Topic} with handler {handlerTypeFullName}.");
                    }
                }
                else
                    _log.LogDebug($"Message {message.Topic} not handled by {handlerTypeFullName}. CanHandle = false.");
            }
            catch (ObjectDisposedException ex)
            {
                // on system shutdown a handler, or even the provider() may throw an ObjectDisposedException, which we accept and log here.
                _log.LogWarning($"Object {ex.ObjectName} disposed while processing message {message.Topic} with handler {handlerTypeFullName}.");
            }
        }

        public void DispatchMessage<TMessageHandler>(Message message) where TMessageHandler : IMessageHandler
            => DispatchMessage(typeof(TMessageHandler).FullName!, message);

        public void DispatchMessage(Action<Message> handler, Message message)
        {
            _log.LogDebug($"Dispatching message {message.Topic} with lambda handler.");
            try
            {
                handler?.Invoke(message);
            }
            catch(Exception e)
            {
                _log.LogError(e, $"Error while processing message {message.Topic} with lambda handler.");
            }
        }

        public void AddHandler(string handlerTypeName, Func<IMessageHandler> handlerFactory)
        {
            _log.LogTrace($"Add message handler {handlerTypeName}.");
            _messageHandlerProviders.Add(handlerTypeName, handlerFactory);
        }

        public void AddHandler<TMessageHandler>(Func<IMessageHandler> handlerFactory) where TMessageHandler : IMessageHandler
            => AddHandler(typeof(TMessageHandler), handlerFactory);

        public void AddHandler(Type handlerType, Func<IMessageHandler> handlerFactory)
            => AddHandler(handlerType.FullName!, handlerFactory);

        public IEnumerable<string> RegisteredHandlers => _messageHandlerProviders.Keys;
    }
}