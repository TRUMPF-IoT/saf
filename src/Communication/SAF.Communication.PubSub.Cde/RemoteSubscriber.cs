// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0


namespace SAF.Communication.PubSub.Cde;
using nsCDEngine.BaseClasses;
using SAF.Common;
using SAF.Messaging.Contracts;
using SAF.Communication.Cde;
using Interfaces;
using SAF.Communication.Cde.Utils;
using MessageProcessing;
using System.Text;
using WireFormat;

/// <summary>
/// Contains the information about a subscriber running on another node.
/// These remote subscribers are managed by <see cref="SubscriptionRegistry"/>.
/// </summary>
internal class RemoteSubscriber : IRemoteSubscriber
{
    private readonly Logger _logger = new(typeof(RemoteSubscriber));

    private readonly ComLine _line;
    private readonly RegistrySubscriptionRequest _registryRequest;
    private readonly ITsmMessageEncoder _encoder;
    private readonly HashSet<string> _patterns;
    private DateTimeOffset _lastActivity = DateTimeOffset.UtcNow;

    private readonly BroadcastMessageQueue _broadcastMessageQueue;

    private const int MaxMessagesPerBlock = 100;
    private const int MaxPayloadBytesPerBlock = 200 * 1024; //200 kB

    private static readonly Version LatestVersion = System.Version.Parse(PubSubVersion.Latest);

    public RemoteSubscriber(ComLine line, TSM tsm, IList<string> patterns, RegistrySubscriptionRequest request, ITsmMessageEncoder encoder)
    {
        Tsm = tsm;

        _line = line;
        _registryRequest = request;
        _encoder = encoder;
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

    /// <summary>
    /// The version messages to this peer are written in: the peer's own, or this node's latest if the peer is newer.
    /// </summary>
    private string WireVersion => System.Version.Parse(Version) > LatestVersion ? PubSubVersion.Latest : Version;

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

        var wireVersion = WireVersion;
        if (!_encoder.CanEncode(message.Message, wireVersion))
        {
            _logger.LogWarning($"Dropped message on {message.Topic.Channel} for {Tsm.ORG}: pub/sub version {wireVersion} cannot transport a message with format {message.Message.GetFormat()}.");
            return;
        }

        if (System.Version.Parse(wireVersion) >= System.Version.Parse(PubSubVersion.V4))
        {
            _broadcastMessageQueue.Enqueue(message);
            return;
        }

        var tsm = CreateBroadcastTsm(message, wireVersion);

        _logger.LogDebug($"Send {MessageToken.Publish} ({message.Topic.Channel}), origin: {_line.Address}, target: {Tsm.ORG}");
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

    private TSM CreateBroadcastTsm(BroadcastMessage message, string wireVersion)
    {
        var messageTxt = $"{MessageToken.Publish}:{new Topic(message.Topic.Channel, message.Topic.MsgId, wireVersion).ToTsmTxt()}";
        var tsm = _encoder.Encode(message.Message, wireVersion).ToTsm(TargetEngine, messageTxt);
        tsm.UID = message.UserId;
        return tsm;
    }

    private void BroadcastQueueProcessing(string userId, IEnumerable<BroadcastMessage> broadcastMessages)
    {
        var messagesToSend = broadcastMessages.Select(m => m.Message);
        var wireVersion = WireVersion;

        foreach (var block in CreateMessageBlocks(messagesToSend))
        {
            var msgId = Guid.NewGuid().ToString("N");
            var messageTxt = $"{MessageToken.Publish}:{new Topic(TsmBatchChannel.Create(block.Count), msgId, wireVersion).ToTsmTxt()}";
            var tsm = _encoder.EncodeBatch(block, wireVersion).ToTsm(TargetEngine, messageTxt);
            tsm.UID = userId;

            _logger.LogDebug($"Send {MessageToken.Publish} (batch size={block.Count}), origin: {_line.Address}, target: {Tsm.ORG}");
            _line.AnswerToSender(Tsm, tsm);
        }
    }

    /// <summary>
    /// Splits an enumeration of messages into blocks. A block is finalized when it reaches
    /// either the maximum number of messages (<see cref="MaxMessagesPerBlock"/>) or the cumulative
    /// payload size - UTF-8 encoded text plus binary - exceeds <see cref="MaxPayloadBytesPerBlock"/>.
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
            var payloadSize = (msg.Payload != null ? Encoding.UTF8.GetByteCount(msg.Payload) : 0) + (msg.BinaryPayload?.Length ?? 0);

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


