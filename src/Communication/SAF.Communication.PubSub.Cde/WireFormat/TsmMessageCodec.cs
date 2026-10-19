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

    public ITsmPeerEncoder ForPeer(string peerVersion) => new TsmPeerEncoder(FormatFor(peerVersion), peerVersion);

    public bool IsBatch(string channel, string version)
        => Version.Parse(version) >= BatchVersion && TsmBatchChannel.Matches(channel);

    public Message? Decode(string channel, string version, TsmPayload payload) => FormatFor(version).Decode(channel, payload);

    public List<Message>? DecodeBatch(string version, TsmPayload payload) => BatchFormatFor(version).DecodeBatch(payload);

    public List<Message>? DecodeMessages(Topic topic, TsmPayload payload)
    {
        if (IsBatch(topic.Channel, topic.Version)) return DecodeBatch(topic.Version, payload);

        return Decode(topic.Channel, topic.Version, payload) is { } message ? [message] : null;
    }

    private ITsmMessageFormat FormatFor(string version)
    {
        var parsed = Version.Parse(version);
        return _formatsNewestFirst.FirstOrDefault(f => f.MinimumVersion <= parsed)
               ?? throw new NotSupportedException($"No C-DEngine message format for pub/sub version {version}.");
    }

    private ITsmBatchFormat BatchFormatFor(string version)
        => FormatFor(version) as ITsmBatchFormat
           ?? throw new NotSupportedException($"Pub/sub version {version} does not support batches.");
}
