// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde;
using Common;
using Interfaces;

public interface ISubscriptionRegistry : IDisposable
{
    public void Broadcast(Topic topic, Message message, string userId, RoutingOptions routingOptions);
    public Task ConnectAsync(CancellationToken token);
}