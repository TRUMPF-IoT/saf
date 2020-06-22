// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;

namespace SAF.Common
{
    public interface IServiceMessageDispatcher
    {
        void DispatchMessage(string handlerTypeFullName, Message message);
        void DispatchMessage<TMessageHandler>(Message message) where TMessageHandler : IMessageHandler;
        void DispatchMessage(Action<Message> handler, Message message);

        /// <summary>
        /// Adds a message handler to the dispatcher.
        /// Usually this is done at assembly scan (AddHost). 
        /// Call this only when using messaging infrastructure outside a hosted service.
        /// </summary>
        /// <param name="handlerFactory">The factory method which provides the handler.</param>
        void AddHandler<TMessageHandler>(Func<IMessageHandler> handlerFactory) where TMessageHandler : IMessageHandler;

        /// <summary>
        /// Adds a message handler to the dispatcher.
        /// Usually this is done at assembly scan (AddHost). 
        /// Call this only when using messaging infrastructure outside a hosted service.
        /// </summary>
        /// <param name="handlerType">The handler type</param>
        /// <param name="handlerFactory">The factory method which provides the handler.</param>
        void AddHandler(Type handlerType, Func<IMessageHandler> handlerFactory);

        /// <summary>
        /// Adds a message handler to the dispatcher.
        /// Usually this is done at assembly scan (AddHost). 
        /// Call this only when using messaging infrastructure outside a hosted service.
        /// </summary>
        /// <param name="handlerTypeName">The full type name of the handler</param>
        /// <param name="handlerFactory">The factory method which provides the handler.</param>
        void AddHandler(string handlerTypeName, Func<IMessageHandler> handlerFactory);
    }
}