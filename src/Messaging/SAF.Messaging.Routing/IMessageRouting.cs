﻿// SPDX-FileCopyrightText: 2017-2021 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Routing;
using Common;

internal interface IMessageRouting
{
    public void Publish(Message message);
    public MessageRoutingSubscription? Subscribe<TMessageHandler>(string routeFilterPattern) where TMessageHandler : IMessageHandler;
    public MessageRoutingSubscription? Subscribe(string routeFilterPattern, Action<Message> handler);
}