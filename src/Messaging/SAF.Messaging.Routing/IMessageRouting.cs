// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Routing;

using SAF.Messaging.Contracts;

internal interface IMessageRouting
{
    public void Publish(Message message);
    public MessageRoutingSubscription? Subscribe<TMessageHandler>(string routeFilterPattern) where TMessageHandler : IMessageHandler;
    public MessageRoutingSubscription? Subscribe(string routeFilterPattern, Action<Message> handler);
}


