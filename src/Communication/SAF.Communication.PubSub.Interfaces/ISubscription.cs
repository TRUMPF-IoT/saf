// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using SAF.Common;

namespace SAF.Communication.PubSub.Interfaces;

public interface ISubscription : IDisposable
{
    Guid Id { get; }

    RoutingOptions RoutingOptions { get; }

    string[] Patterns { get; }

    void SetHandler(Action<DateTimeOffset, Message> handler);

    void Unsubscribe();
}