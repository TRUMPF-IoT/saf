// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Collections.Concurrent;

namespace SAF.Messaging.Nats;

public class NatsSubscriptionManager : INatsSubscriptionManager
{
    private readonly ConcurrentDictionary<Guid, (string routeFilterPattern, CancellationTokenSource cancellationTokenSource, Task)> _subscriptions = new();

    public bool TryAdd(Guid subscriptionId, (string routeFilterPattern, CancellationTokenSource cancellationTokenSource, Task) subscription)
        => _subscriptions.TryAdd(subscriptionId, subscription);

    public bool TryRemove(Guid subscriptionId, out (string routeFilterPattern, CancellationTokenSource cancellationTokenSource, Task) subscription)
        => _subscriptions.TryRemove(subscriptionId, out subscription);
}
