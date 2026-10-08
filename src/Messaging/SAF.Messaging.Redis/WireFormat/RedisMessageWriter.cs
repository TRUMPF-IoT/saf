// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

using System.Diagnostics.CodeAnalysis;
using SAF.Messaging.Contracts;
using StackExchange.Redis;

/// <summary>
/// Writes each message in the oldest format that can carry it, so older nodes can read as much as possible.
/// </summary>
internal sealed class RedisMessageWriter : IRedisMessageWriter
{
    private readonly IReadOnlyList<IRedisEnvelopeFormat> _formats;
    private readonly IRedisBinaryFrame _binaryFrame;

    public RedisMessageWriter(IEnumerable<IRedisEnvelopeFormat> formats, IRedisBinaryFrame binaryFrame)
    {
        _formats = formats.OrderBy(f => f.MajorVersions.Max()).ToList();
        _binaryFrame = binaryFrame;
    }

    public bool TryWrite(Message message, out RedisValue value, [NotNullWhen(false)] out string? dropReason)
    {
        var format = _formats.FirstOrDefault(f => f.CanWrite(message));
        if (format == null)
        {
            value = RedisValue.Null;
            dropReason = $"Redis messaging has no wire format for a message with format {message.GetFormat()}.";
            return false;
        }

        var wireValue = format.Write(message);
        // Without a binary payload the envelope stays plain JSON, which every SAF version reads.
        value = wireValue.BinaryPayload is null ? wireValue.Envelope : _binaryFrame.Write(wireValue.Envelope, wireValue.BinaryPayload);
        dropReason = null;
        return true;
    }
}
