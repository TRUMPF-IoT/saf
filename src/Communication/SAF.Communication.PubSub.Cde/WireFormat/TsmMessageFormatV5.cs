// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

using nsCDEngine.BaseClasses;
using SAF.Messaging.Contracts;
using Interfaces;

/// <summary>
/// The JSON of <see cref="TsmMessageFormatV2"/> in <c>PLS</c> and the binary payloads in <c>PLB</c>, used from
/// <see cref="PubSubVersion.V5"/> on. Each message names the length of its binary payload; a batch carries the
/// binary payloads of its messages back to back, in message order. <c>PLB</c> has an odd length, padded with one
/// byte if needed.
/// </summary>
/// <remarks>
/// <c>PLS</c> is never empty, otherwise C-DEngine would read <c>PLB</c> as a compressed <c>PLS</c>.
/// C-DEngine (6.112.2 and older) loses a <c>PLB</c> whose length is a whole multiple of its chunk size: it announces
/// one chunk more than it sends. Its chunk sizes are even, so an odd length is never affected.
/// A 9.x registry writes the subscriber's version into the TSM text but sends <see cref="TsmMessageFormatV2"/>
/// JSON, so messages without binary payloads must stay readable without <c>PLB</c>.
/// </remarks>
internal sealed class TsmMessageFormatV5 : ITsmBatchFormat
{
    public Version MinimumVersion { get; } = Version.Parse(PubSubVersion.V5);

    public bool CanEncode(Message message) => true;

    public TsmPayload Encode(Message message)
        => new(TheCommonUtils.SerializeObjectToJSONString(MessageDtoV2.FromMessage(message)), JoinBinaryPayloads([message]));

    public Message? Decode(string channel, TsmPayload payload)
    {
        var dto = TheCommonUtils.DeserializeJSONStringToObject<MessageDtoV2>(payload.Pls);
        return dto == null ? null : Read([dto], payload.Plb)?[0];
    }

    public TsmPayload EncodeBatch(IEnumerable<Message> messages)
    {
        var list = messages.ToList();
        return new(TheCommonUtils.SerializeObjectToJSONString(list.Select(MessageDtoV2.FromMessage).ToList()), JoinBinaryPayloads(list));
    }

    public List<Message>? DecodeBatch(TsmPayload payload)
    {
        var dtos = TheCommonUtils.DeserializeJSONStringToObject<List<MessageDtoV2>>(payload.Pls);
        return dtos == null ? null : Read(dtos, payload.Plb);
    }

    // C-DEngine drops an empty PLB, so an empty binary payload travels as its length alone.
    private static byte[]? JoinBinaryPayloads(IReadOnlyList<Message> messages)
    {
        var parts = messages.Select(m => m.BinaryPayload).OfType<byte[]>().Where(b => b.Length > 0).ToList();
        var length = parts.Sum(p => p.Length);
        if (length == 0) return null;
        if (parts.Count == 1 && length == PlbLength(length)) return parts[0];

        var plb = new byte[PlbLength(length)];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(plb, offset);
            offset += part.Length;
        }
        return plb;
    }

    // Returns null if the announced lengths do not add up to PLB.
    private static List<Message>? Read(IReadOnlyList<MessageDtoV2> dtos, byte[]? plb)
    {
        var available = plb?.Length ?? 0;
        var offset = 0;
        var messages = new List<Message>(dtos.Count);
        foreach (var dto in dtos)
        {
            var message = dto.ToMessage();
            if (dto.BinaryPayloadLength is { } length)
            {
                if (length < 0 || length > available - offset) return null;
                message.BinaryPayload = Slice(plb, offset, length);
                offset += length;
            }
            messages.Add(message);
        }
        return available == PlbLength(offset) ? messages : null;
    }

    private static int PlbLength(int payloadLength) => payloadLength == 0 ? 0 : payloadLength | 1;

    private static byte[] Slice(byte[]? plb, int offset, int length)
    {
        if (length == 0) return [];
        return length == plb!.Length ? plb : plb.AsSpan(offset, length).ToArray();
    }
}
