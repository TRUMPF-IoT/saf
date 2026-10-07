// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

using SAF.Messaging.Contracts;

/// <summary>
/// Wire shape of the <see cref="NatsHeaderNames.Metadata"/> header in version 2.
/// Add optional fields with a minor version bump, anything else needs a new major version.
/// </summary>
internal sealed class MessageMetadataDtoV2
{
    public List<MessageCustomPropertyDtoV2>? CustomProperties { get; set; }

    public static MessageMetadataDtoV2 FromMessage(Message message)
        => new()
        {
            CustomProperties = message.CustomProperties?
                .Select(p => new MessageCustomPropertyDtoV2 { Name = p.Name, Value = p.Value })
                .ToList()
        };

    public List<MessageCustomProperty>? ToCustomProperties()
        => CustomProperties?
            .Select(p => new MessageCustomProperty { Name = p.Name!, Value = p.Value })
            .ToList();
}

internal sealed class MessageCustomPropertyDtoV2
{
    public string? Name { get; set; }
    public string? Value { get; set; }
}
