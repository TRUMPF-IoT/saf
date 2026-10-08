// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

using SAF.Messaging.Contracts;

/// <summary>
/// Wire shape of a message for peers from <see cref="Interfaces.PubSubVersion.V2"/> on. Old nodes ignore fields
/// they do not know, so only optional fields may be added - anything else needs a new
/// <see cref="Interfaces.PubSubVersion"/>.
/// </summary>
internal sealed class MessageDtoV2
{
    public string? Topic { get; set; }
    public string? Payload { get; set; }
    public int? AcceptedReplyFormats { get; set; }
    public List<MessageCustomPropertyDtoV2>? CustomProperties { get; set; }

    /// <summary>
    /// From <see cref="Interfaces.PubSubVersion.V5"/> on: the length of the message's binary payload in the TSM's
    /// <c>PLB</c>; <c>null</c> if it has none.
    /// </summary>
    public int? BinaryPayloadLength { get; set; }

    public static MessageDtoV2 FromMessage(Message message)
        => new()
        {
            Topic = message.Topic,
            Payload = message.Payload,
            AcceptedReplyFormats = (int?)message.AcceptedReplyFormats,
            CustomProperties = message.CustomProperties?
                .Select(p => new MessageCustomPropertyDtoV2 { Name = p.Name, Value = p.Value })
                .ToList(),
            BinaryPayloadLength = message.BinaryPayload?.Length
        };

    public Message ToMessage()
        => new()
        {
            Topic = Topic!,
            Payload = Payload,
            AcceptedReplyFormats = (MessageFormats?)AcceptedReplyFormats,
            CustomProperties = CustomProperties?
                .Select(p => new MessageCustomProperty { Name = p.Name!, Value = p.Value })
                .ToList()
        };
}

internal sealed class MessageCustomPropertyDtoV2
{
    public string? Name { get; set; }
    public string? Value { get; set; }
}
