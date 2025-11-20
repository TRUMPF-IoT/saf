// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Common;

public interface IServiceMessageDispatcher
{
    /// <summary>
    /// Adds a message handler to the message dispatcher.
    /// Usually this is done at assembly scan type during start up of a SAF service host.
    /// Call this only when using messaging infrastructure outside a hosted service.
    /// </summary>
    /// <param name="handlerFactory">The factory method which provides the handler.</param>
    void AddHandler<TMessageHandler>(Func<IMessageHandler> handlerFactory) where TMessageHandler : IMessageHandler;

    /// <summary>
    /// Adds a message handler to the message dispatcher.
    /// Usually this is done at assembly scan type during start up of a SAF service host.
    /// Call this only when using messaging infrastructure outside a hosted service.
    /// </summary>
    /// <param name="handlerType">The handler type.</param>
    /// <param name="handlerFactory">The factory method which provides the handler.</param>
    void AddHandler(Type handlerType, Func<IMessageHandler> handlerFactory);

    /// <summary>
    /// Adds a message handler to the message dispatcher.
    /// Usually this is done at assembly scan type during start up of a SAF service host.
    /// Call this only when using messaging infrastructure outside a hosted service.
    /// </summary>
    /// <param name="handlerTypeName">The full type name of the handler type.</param>
    /// <param name="handlerFactory">The factory method which provides the handler.</param>
    void AddHandler(string handlerTypeName, Func<IMessageHandler> handlerFactory);

    void DispatchMessage<TMessageHandler>(Message message) where TMessageHandler : IMessageHandler;
    void DispatchMessage(Type handlerType, Message message);
    void DispatchMessage(string handlerTypeFullName, Message message);
    void DispatchMessage(Action<Message> handler, Message message);
}