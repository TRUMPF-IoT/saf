// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using nsCDEngine.BaseClasses;
using SAF.Common;
using SAF.Communication.Cde;
using SAF.Communication.PubSub.Interfaces;
using SAF.Communication.Cde.Utils;
using SAF.Communication.PubSub.Cde.MessageProcessing;
using System.Text;

namespace SAF.Communication.PubSub.Cde;

/// <summary>
/// Contains the information about a subscriber running on another node.
/// These remote subscribers are managed by <see cref="SubscriptionRegistry"/>.
/// </summary>
internal class RemoteSubscriber : IRemoteSubscriber
{
    private readonly Logger _logger = new(typeof(RemoteSubscriber));

    private readonly ComLine _line;
    private readonly RegistrySubscriptionRequest _registryRequest;
    private readonly HashSet<string> _patterns;
    private DateTimeOffset _lastActivity = DateTimeOffset.UtcNow;

    private readonly BroadcastMessageQueue _broadcastMessageQueue;

    private const int MaxMessagesPerBlock = 100;
    private const int MaxPayloadBytesPerBlock = 200 * 1024; //200 kB

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

    public bool IsRoutingAllowed(RoutingOptions routingOptions)
        => routingOptions switch
        {
            RoutingOptions.All => true,
            RoutingOptions.Local => IsLocalHost,
            RoutingOptions.Remote => !IsLocalHost,
            _ => throw new ArgumentOutOfRangeException(nameof(routingOptions))
        };

    private void BroadcastQueueProcessing(string userId, IEnumerable<BroadcastMessage> broadcastMessages)
    {
        var messagesToSend = broadcastMessages.Select(m => m.Message);

        foreach (var block in CreateMessageBlocks(messagesToSend))
        {
            var serializedMessages = TheCommonUtils.SerializeObjectToJSONString(block);

            var msgId = Guid.NewGuid().ToString("N");
            var messageTxt = $"{MessageToken.Publish}:{new Topic($"$$batch:size={block.Count}$$", msgId, Version).ToTsmTxt()}";
            var tsm = new TSM(TargetEngine, messageTxt, serializedMessages)
            {
                UID = userId
            };

            _logger.LogDebug($"Send {MessageToken.Publish} (batch size={block.Count}), origin: {_line.Address}, target: {Tsm.ORG}");
            _line.AnswerToSender(Tsm, tsm);
        }
    }

    /// <summary>
    /// Splits an enumeration of messages into blocks. A block is finalized when it reaches
    /// either the maximum number of messages (<see cref="MaxMessagesPerBlock"/>) or the cumulative
    /// UTF-8 encoded payload size exceeds <see cref="MaxPayloadBytesPerBlock"/>.
    /// A single message larger than the payload limit will be placed in its own block.
    /// </summary>
    /// <param name="messages">The messages to split.</param>
    /// <returns>An enumeration of blocks, each block being a read-only list of messages.</returns>
    private static IEnumerable<IReadOnlyList<Message>> CreateMessageBlocks(IEnumerable<Message> messages)
    {
        var block = new List<Message>(MaxMessagesPerBlock);
        var cumulativeSize = 0;

        foreach (var msg in messages)
        {
            var payloadSize = msg.Payload != null ? Encoding.UTF8.GetByteCount(msg.Payload) : 0;

            if (block.Count > 0 && (block.Count == MaxMessagesPerBlock || cumulativeSize + payloadSize > MaxPayloadBytesPerBlock))
            {
                yield return block;

                block = new List<Message>(MaxMessagesPerBlock);
                cumulativeSize = 0;
            }

            block.Add(msg);
            cumulativeSize += payloadSize;

            if (block.Count == MaxMessagesPerBlock || cumulativeSize >= MaxPayloadBytesPerBlock)
            {
                yield return block;
                block = new List<Message>(MaxMessagesPerBlock);
                cumulativeSize = 0;
            }
        }

        if (block.Count > 0)
        {
            yield return block;
        }
    }
}