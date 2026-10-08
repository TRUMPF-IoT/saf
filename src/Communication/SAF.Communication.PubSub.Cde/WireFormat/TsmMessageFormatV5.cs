// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

using nsCDEngine.BaseClasses;
using SAF.Messaging.Contracts;
using Interfaces;

/// <summary>
/// The JSON of <see cref="TsmMessageFormatV2"/> in <c>PLS</c> and the binary payloads in <c>PLB</c>, used from
/// <see cref="PubSubVersion.V5"/> on. Each message names the length of its binary payload; the injected
/// <see cref="ITsmPlbLayout"/> places the binary payloads in <c>PLB</c>.
/// </summary>
/// <remarks>
/// <c>PLS</c> is never empty, otherwise C-DEngine would read <c>PLB</c> as a compressed <c>PLS</c>.
/// A 9.x registry writes the subscriber's version into the TSM text but sends <see cref="TsmMessageFormatV2"/>
/// JSON, so messages without binary payloads must stay readable without <c>PLB</c>.
/// </remarks>
internal sealed class TsmMessageFormatV5(ITsmPlbLayout plbLayout) : ITsmBatchFormat
{
    public Version MinimumVersion { get; } = Version.Parse(PubSubVersion.V5);

    public bool CanEncode(Message message) => true;

    public TsmPayload Encode(Message message)
        => new(TheCommonUtils.SerializeObjectToJSONString(MessageDtoV2.FromMessage(message)), plbLayout.Pack(BinaryPayloadsOf([message])));

    public Message? Decode(string channel, TsmPayload payload)
    {
        var dto = TheCommonUtils.DeserializeJSONStringToObject<MessageDtoV2>(payload.Pls);
        return dto == null ? null : Read([dto], payload.Plb)?[0];
    }

    public TsmPayload EncodeBatch(IEnumerable<Message> messages)
    {
        var list = messages.ToList();
        return new(TheCommonUtils.SerializeObjectToJSONString(list.Select(MessageDtoV2.FromMessage).ToList()), plbLayout.Pack(BinaryPayloadsOf(list)));
    }

    public List<Message>? DecodeBatch(TsmPayload payload)
    {
        var dtos = TheCommonUtils.DeserializeJSONStringToObject<List<MessageDtoV2>>(payload.Pls);
        return dtos == null ? null : Read(dtos, payload.Plb);
    }

    private static IReadOnlyList<byte[]> BinaryPayloadsOf(IEnumerable<Message> messages)
        => messages.Select(m => m.BinaryPayload).OfType<byte[]>().ToList();

    // Returns null if the announced lengths do not match PLB.
    private List<Message>? Read(IReadOnlyList<MessageDtoV2> dtos, byte[]? plb)
    {
        var binaryPayloads = plbLayout.Unpack(plb, dtos.Select(d => d.BinaryPayloadLength).OfType<int>().ToList());
        if (binaryPayloads == null) return null;

        var next = 0;
        var messages = new List<Message>(dtos.Count);
        foreach (var dto in dtos)
        {
            var message = dto.ToMessage();
            if (dto.BinaryPayloadLength != null) message.BinaryPayload = binaryPayloads[next++];
            messages.Add(message);
        }
        return messages;
    }
}
