// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.RequestClient;

using System.Collections.Concurrent;
using SAF.Messaging.Contracts;

/// <summary>
/// Reply topics <c>{prefix}/{request id}/requestclient/{client id}</c>, received by one subscription per prefix:
/// <c>{prefix}/*/requestclient/{client id}</c>. The wildcard stands for exactly one topic level, which every transport
/// can express; NATS cannot match a leading wildcard over several levels.
/// </summary>
internal sealed class ReplyTopics(IMessagingInfrastructure messaging, Action<Message> handleReply) : IReplyTopics
{
    private readonly string _postfix = $"requestclient/{Guid.NewGuid()}";

    // Subscribing can block for seconds (C-DEngine waits for its registries), so requests on other prefixes must
    // not wait for it. Requests on the same prefix do: their replies may arrive only once the subscription is in place.
    private readonly ConcurrentDictionary<string, Lazy<object?>> _subscriptions = new();

    public void Subscribe(string prefix)
    {
        prefix = prefix.TrimEnd('/');
        var subscription = _subscriptions.GetOrAdd(prefix,
            p => new Lazy<object?>(() => messaging.Subscribe($"{p}/*/{_postfix}", handleReply)));
        try
        {
            _ = subscription.Value;
        }
        catch
        {
            // Lazy keeps the exception, so the next request on the prefix needs a new one.
            _subscriptions.TryRemove(new KeyValuePair<string, Lazy<object?>>(prefix, subscription));
            throw;
        }
    }

    public string Create(string prefix)
    {
        Subscribe(prefix);
        return $"{prefix.TrimEnd('/')}/{Guid.NewGuid()}/{_postfix}";
    }

    public void Dispose()
    {
        foreach (var prefix in _subscriptions.Keys)
        {
            if (!_subscriptions.TryRemove(prefix, out var subscription)) continue;

            object? handle;
            try
            {
                // A subscription still in progress is awaited, so it does not outlive the client.
                handle = subscription.Value;
            }
            catch
            {
                // The request that subscribed got the exception; the other prefixes still need unsubscribing.
                continue;
            }

            if (handle is not null) messaging.Unsubscribe(handle);
        }
    }
}
