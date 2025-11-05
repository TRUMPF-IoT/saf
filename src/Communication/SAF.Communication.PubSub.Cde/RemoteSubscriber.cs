// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using nsCDEngine.BaseClasses;
using SAF.Common;
using SAF.Communication.Cde;
using SAF.Communication.PubSub.Interfaces;
using SAF.Communication.Cde.Utils;
using SAF.Communication.PubSub.Cde.MessageProcessing;

namespace SAF.Communication.PubSub.Cde;

/// <summary>
/// Contains the information about a subscriber running on another node.
/// These remote subscibers are manged by <see cref="SubscriptionRegistry"/>.
/// </summary>
internal class RemoteSubscriber : IRemoteSubscriber
{
    private readonly Logger _logger = new(typeof(RemoteSubscriber));

    private readonly ComLine _line;
    private readonly RegistrySubscriptionRequest _registryRequest;
    private readonly HashSet<string> _patterns;
    private DateTimeOffset _lastActivity = DateTimeOffset.UtcNow;

    private readonly BroadcastMessageQueue _broadcastMessageQueue;

    public RemoteSubscriber(ComLine line, TSM tsm)
        : this(line, tsm, new List<string>(), new RegistrySubscriptionRequest())
    { }
    public RemoteSubscriber(ComLine line, TSM tsm, IList<string> patterns, RegistrySubscriptionRequest request)
    {
        Tsm = tsm;

        _line = line;
        _registryRequest = request;
        _patterns = [..patterns.Distinct()];
        IsLocalHost = tsm.IsLocalHost();

        _broadcastMessageQueue = new BroadcastMessageQueue(BroadcastQueueProcessing);
    }

    public TSM Tsm { get; }
    public bool IsLocalHost { get; }
    public bool IsAlive => DateTimeOffset.UtcNow - _lastActivity <= TimeSpan.FromSeconds(Subscriber.AliveIntervalSeconds * 2);
    public bool IsRegistry => _registryRequest.isRegistry;
    public string TargetEngine => IsRegistry ? Engines.PubSub : Engines.RemotePubSub;

    public string Version => string.IsNullOrEmpty(_registryRequest.version)
        ? PubSubVersion.V1
        : _registryRequest.version;

    public void AddPatterns(IList<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            _patterns.Add(pattern);
        }
    }

    public void RemovePatterns(IList<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            _patterns.Remove(pattern);
        }
    }

    public bool HasPatterns => _patterns.Count != 0;

    public bool IsMatch(string topic)
        => _patterns.Contains("*") || _patterns.Contains(topic) || _patterns.Any(topic.IsMatch);

    public void Touch() => _lastActivity = DateTimeOffset.UtcNow;

    public void Broadcast(BroadcastMessage message)
    {
        if (!IsRoutingAllowed(message.RoutingOptions) ||
            !IsMatch(message.Topic.Channel))
        {
            return;
        }

        if (System.Version.Parse(Version) >= System.Version.Parse(PubSubVersion.V4))
        {
            _broadcastMessageQueue.Enqueue(message);
            return;
        }

        var tsm = CreateBroadcastTsm(message);

        _logger.LogDebug($"Send {MessageToken.Publish} ({message.Topic.Channel}), origin: {_line.Address}, target: {Tsm.ORG}");
        _line.AnswerToSender(Tsm, tsm);
    }

    private TSM CreateBroadcastTsm(BroadcastMessage message)
    {
        TSM tsm;
        var messageTxt = $"{MessageToken.Publish}:{new Topic(message.Topic.Channel, message.Topic.MsgId, Version).ToTsmTxt()}";
        if (Version == PubSubVersion.V1)
        {
            tsm = new TSM(TargetEngine, messageTxt, message.Message.Payload)
            {
                UID = message.UserId
            };
        }
        else
        {
            tsm = new TSM(TargetEngine, messageTxt, TheCommonUtils.SerializeObjectToJSONString(message.Message))
            {
                UID = message.UserId
            };
        }

        return tsm;
    }

    private void BroadcastQueueProcessing(string userId, IEnumerable<BroadcastMessage> broadcastMessages)
    {
        var messagesToSend = broadcastMessages.Select(m => m.Message);
        var serializedMessages = TheCommonUtils.SerializeObjectToJSONString(messagesToSend);

        var msgId = Guid.NewGuid().ToString("N");
        var messageTxt = $"{MessageToken.Publish}:{new Topic("$$batch$$", msgId, Version).ToTsmTxt()}";
        var tsm = new TSM(TargetEngine, messageTxt, serializedMessages)
        {
            UID = userId
        };

        _logger.LogDebug($"Send {MessageToken.Publish} (batch), origin: {_line.Address}, target: {Tsm.ORG}");
        _line.AnswerToSender(Tsm, tsm);
    }

    public bool IsRoutingAllowed(RoutingOptions routingOptions)
        => routingOptions switch
        {
            RoutingOptions.All => true,
            RoutingOptions.Local => IsLocalHost,
            RoutingOptions.Remote => !IsLocalHost,
            _ => throw new ArgumentOutOfRangeException(nameof(routingOptions))
        };
}