// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.WireFormat;

using System.Diagnostics.CodeAnalysis;
using SAF.Messaging.Contracts;

/// <summary>
/// Drops messages with a binary payload, which nodes before SAF 11 would read as corrupt text, and passes all others on.
/// </summary>
public sealed class BinaryPayloadsDisabledWriter<TWire>(IWireMessageWriter<TWire> writer) : IWireMessageWriter<TWire>
{
    public MessageFormats Formats => writer.Formats & ~MessageFormats.Binary;

    public bool TryWrite(Message message, out TWire wire, [NotNullWhen(false)] out string? dropReason)
    {
        if (message.BinaryPayload is null) return writer.TryWrite(message, out wire, out dropReason);

        wire = default!;
        dropReason = "Sending binary payloads is disabled by the setting EnableBinaryPayloads.";
        return false;
    }
}
