// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Common;

namespace SAF.Communication.PubSub.Interfaces
{
    public interface IPublisher
    {
        void Publish(string channel, string message);
        void Publish(string channel, string message, RoutingOptions routingOptions);
        void Publish(Message message);
        void Publish(Message message, RoutingOptions routingOptions);
        void Publish(Message message, Guid userId);
        void Publish(Message message, Guid userId, RoutingOptions routingOptions);
        void Publish(Message message, string userId);
        void Publish(Message message, string userId, RoutingOptions routingOptions);
    }
}