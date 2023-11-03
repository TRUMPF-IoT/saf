// SPDX-FileCopyrightText: 2017-2021 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Common;

namespace SAF.Messaging.Routing
{
    internal interface IMessageRouting
    {
        public void Publish(Message message);
        public MessageRoutingSubscription? Subscribe<TMessageHandler>(string routeFilterPattern) where TMessageHandler : IMessageHandler;
        public MessageRoutingSubscription? Subscribe(string routeFilterPattern, Action<Message> handler);
    }
}
