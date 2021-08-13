// SPDX-FileCopyrightText: 2017-2021 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Common;
using SAF.Communication.PubSub.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAF.Communication.PubSub.Cde
{
    public interface ISubscriptionRegistry : IDisposable
    {
        public void Broadcast(Topic topic, Message message, string userId, RoutingOptions routingOptions);
        public Task ConnectAsync(CancellationToken token);
    }
}
