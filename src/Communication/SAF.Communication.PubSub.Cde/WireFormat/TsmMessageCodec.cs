// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

using SAF.Messaging.Contracts;
using Interfaces;

/// <summary>
/// Picks the message format for a pub/sub version: the newest one whose minimum version the peer reaches.
/// </summary>
internal sealed class TsmMessageCodec : ITsmMessageEncoder, ITsmMessageDecoder
{
    private static readonly Version BatchVersion = Version.Parse(PubSubVersion.V4);

    private readonly IReadOnlyList<ITsmMessageFormat> _formatsNewestFirst;

    public TsmMessageCodec(IEnumerable<ITsmMessageFormat> formats)
    {
        _formatsNewestFirst = formats.OrderByDescending(f => f.MinimumVersion).ToList();
    }

    public bool CanEncode(Message message, string peerVersion) => FormatFor(peerVersion).CanEncode(message);

    public TsmPayload Encode(Message message, string peerVersion)
    {
        var format = FormatFor(peerVersion);
        EnsureCanEncode(format, message, peerVersion);
        return format.Encode(message);
    }

    public TsmPayload EncodeBatch(IEnumerable<Message> messages, string peerVersion)
    {
        var format = BatchFormatFor(peerVersion);
        var list = messages.ToList();
        foreach (var message in list) EnsureCanEncode(format, message, peerVersion);
        return format.EncodeBatch(list);
    }

    public bool IsBatch(string channel, string version)
        => Version.Parse(version) >= BatchVersion && TsmBatchChannel.Matches(channel);

    public Message? Decode(string channel, string version, TsmPayload payload) => FormatFor(version).Decode(channel, payload);

    public List<Message>? DecodeBatch(string version, TsmPayload payload) => BatchFormatFor(version).DecodeBatch(payload);

    public List<Message>? DecodeMessages(Topic topic, TsmPayload payload)
        => IsBatch(topic.Channel, topic.Version)
            ? DecodeBatch(topic.Version, payload)
            : Decode(topic.Channel, topic.Version, payload) is { } message ? [message] : null;

    private ITsmMessageFormat FormatFor(string version)
    {
        var parsed = Version.Parse(version);
        return _formatsNewestFirst.FirstOrDefault(f => f.MinimumVersion <= parsed)
               ?? throw new NotSupportedException($"No C-DEngine message format for pub/sub version {version}.");
    }

    private static void EnsureCanEncode(ITsmMessageFormat format, Message message, string peerVersion)
    {
        if (!format.CanEncode(message))
            throw new InvalidOperationException($"Pub/sub version {peerVersion} cannot carry the message on {message.Topic}.");
    }

    private ITsmBatchFormat BatchFormatFor(string version)
        => FormatFor(version) as ITsmBatchFormat
           ?? throw new NotSupportedException($"Pub/sub version {version} does not support batches.");
}
