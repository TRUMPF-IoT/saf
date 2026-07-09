// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0


namespace SAF.Communication.PubSub.Interfaces;
using Common;

public interface ISubscription : IDisposable
{
    Guid Id { get; }

    RoutingOptions RoutingOptions { get; }

    string[] Patterns { get; }

    void SetHandler(Action<DateTimeOffset, Message> handler);

    void Unsubscribe();
}