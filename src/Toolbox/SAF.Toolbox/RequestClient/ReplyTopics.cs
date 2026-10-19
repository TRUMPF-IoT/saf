// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.RequestClient;

using SAF.Messaging.Contracts;

/// <summary>
/// Reply topics <c>{prefix}/{request id}/requestclient/{client id}</c>, received under every prefix by one
/// subscription <c>*/requestclient/{client id}</c>, as in SAF 10.x. NATS matches that leading wildcard against
/// exactly one level, so it receives no replies there without routing.
/// </summary>
internal sealed class ReplyTopics(IMessagingInfrastructure messaging, Action<Message> handleReply) : IReplyTopics
{
    private readonly string _postfix = $"requestclient/{Guid.NewGuid()}";

    // Null until the first subscription and after a failed one. Requests wait for a subscription in progress: their
    // replies may arrive only once it is in place.
    private Lazy<object?>? _subscription;

    public void Subscribe()
    {
        var subscription = LazyInitializer.EnsureInitialized(ref _subscription,
            () => new Lazy<object?>(() => messaging.Subscribe($"*/{_postfix}", handleReply)));
        try
        {
            _ = subscription.Value;
        }
        catch
        {
            // Lazy keeps the exception, so the next request needs a new one.
            Interlocked.CompareExchange(ref _subscription, null, subscription);
            throw;
        }
    }

    public string Create(string prefix)
    {
        Subscribe();
        return $"{prefix.TrimEnd('/')}/{Guid.NewGuid()}/{_postfix}";
    }

    public void Dispose()
    {
        var subscription = Interlocked.Exchange(ref _subscription, null);
        if (subscription is null) return;

        object? handle;
        try
        {
            // A subscription still in progress is awaited, so it does not outlive the client.
            handle = subscription.Value;
        }
        catch
        {
            // The request that subscribed got the exception.
            return;
        }

        if (handle is not null) messaging.Unsubscribe(handle);
    }
}
