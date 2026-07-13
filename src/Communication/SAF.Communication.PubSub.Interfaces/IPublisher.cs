// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Interfaces;

using SAF.Messaging.Contracts;

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


