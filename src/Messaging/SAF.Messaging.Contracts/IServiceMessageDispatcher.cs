// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Contracts;

public interface IServiceMessageDispatcher
{
    void DispatchMessage<TMessageHandler>(Message message) where TMessageHandler : IMessageHandler;
    void DispatchMessage(Type handlerType, Message message);
    void DispatchMessage(Action<Message> handler, Message message);
}

