// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

using System.Diagnostics.CodeAnalysis;
using SAF.Messaging.Contracts;

/// <summary>
/// Drops messages with a binary payload, which nodes before SAF 11 would read as corrupt text, and passes all others on.
/// </summary>
internal sealed class BinaryPayloadsDisabledWriter(INatsMessageWriter writer) : INatsMessageWriter
{
    public bool TryWrite(Message message, out NatsWireMessage wireMessage, [NotNullWhen(false)] out string? dropReason)
    {
        if (message.BinaryPayload is null) return writer.TryWrite(message, out wireMessage, out dropReason);

        wireMessage = default;
        dropReason = "Sending binary payloads is disabled by the setting EnableBinaryPayloads.";
        return false;
    }
}
