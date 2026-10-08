// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

using SAF.Messaging.Contracts;

/// <summary>
/// Wire shape of the <see cref="NatsHeaderNames.Metadata"/> header from version 2 on.
/// Older nodes ignore fields they do not know, so only optional fields may be added - anything else
/// needs a new major version.
/// </summary>
internal sealed class MessageMetadataDtoV2
{
    /// <summary>
    /// From version 3 on: the text payload, because the body carries the binary payload.
    /// </summary>
    public string? Payload { get; set; }

    public int? AcceptedReplyFormats { get; set; }
    public List<MessageCustomPropertyDtoV2>? CustomProperties { get; set; }

    // A method, so the serializer does not write it.
    public bool IsEmpty() => Payload is null && AcceptedReplyFormats is null && CustomProperties is null;

    public static MessageMetadataDtoV2 FromMessage(Message message)
        => new()
        {
            AcceptedReplyFormats = (int?)message.AcceptedReplyFormats,
            CustomProperties = message.CustomProperties?
                .Select(p => new MessageCustomPropertyDtoV2 { Name = p.Name, Value = p.Value })
                .ToList()
        };

    public Message ToMessage(string topic, string? payload)
        => new()
        {
            Topic = topic,
            Payload = payload,
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
