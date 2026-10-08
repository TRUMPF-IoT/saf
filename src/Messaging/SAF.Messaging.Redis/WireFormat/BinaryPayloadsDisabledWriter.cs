// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

using System.Diagnostics.CodeAnalysis;
using SAF.Messaging.Contracts;
using StackExchange.Redis;

/// <summary>
/// Drops messages with a binary payload, which nodes before SAF 11 would read as corrupt text, and passes all others on.
/// </summary>
internal sealed class BinaryPayloadsDisabledWriter(IRedisMessageWriter writer) : IRedisMessageWriter
{
    public bool TryWrite(Message message, out RedisValue value, [NotNullWhen(false)] out string? dropReason)
    {
        if (message.BinaryPayload is null) return writer.TryWrite(message, out value, out dropReason);

        value = RedisValue.Null;
        dropReason = "Sending binary payloads is disabled by the setting EnableBinaryPayloads.";
        return false;
    }
}
