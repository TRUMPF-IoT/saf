// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

using SAF.Messaging.Contracts;

/// <summary>
/// Wire shape of a message for peers from <see cref="Interfaces.PubSubVersion.V2"/> up to
/// <see cref="Interfaces.PubSubVersion.V4"/>. Old nodes read exactly this shape, so it must not change -
/// introduce a new <see cref="Interfaces.PubSubVersion"/> instead.
/// </summary>
internal sealed class MessageDtoV2
{
    public string? Topic { get; set; }
    public string? Payload { get; set; }
    public List<MessageCustomPropertyDtoV2>? CustomProperties { get; set; }

    public static MessageDtoV2 FromMessage(Message message)
        => new()
        {
            Topic = message.Topic,
            Payload = message.Payload,
            CustomProperties = message.CustomProperties?
                .Select(p => new MessageCustomPropertyDtoV2 { Name = p.Name, Value = p.Value })
                .ToList()
        };

    public Message ToMessage()
        => new()
        {
            Topic = Topic!,
            Payload = Payload,
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
