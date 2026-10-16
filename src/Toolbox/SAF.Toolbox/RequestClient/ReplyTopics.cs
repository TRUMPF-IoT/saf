// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.RequestClient;
using SAF.Messaging.Contracts;

/// <summary>
/// Reply topics <c>{prefix}/{request id}/requestclient/{client id}</c>, received by one subscription per prefix:
/// <c>{prefix}/*/requestclient/{client id}</c>. The wildcard stands for exactly one topic level, which every transport
/// can express; NATS cannot match a leading wildcard over several levels.
/// </summary>
internal sealed class ReplyTopics(IMessagingInfrastructure messaging, Action<Message> handleReply) : IReplyTopics
{
    private readonly string _postfix = $"requestclient/{Guid.NewGuid()}";
    private readonly Dictionary<string, object?> _subscriptions = new();

    public string Create(string prefix)
    {
        prefix = prefix.TrimEnd('/');
        // Subscribing returns once the subscription is in place, so the first reply is not missed.
        lock (_subscriptions)
        {
            if (!_subscriptions.ContainsKey(prefix))
                _subscriptions[prefix] = messaging.Subscribe($"{prefix}/*/{_postfix}", handleReply);
        }

        return $"{prefix}/{Guid.NewGuid()}/{_postfix}";
    }

    public void Dispose()
    {
        lock (_subscriptions)
        {
            foreach (var subscription in _subscriptions.Values.OfType<object>())
                messaging.Unsubscribe(subscription);
            _subscriptions.Clear();
        }
    }
}
