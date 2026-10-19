// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

using System.Diagnostics.CodeAnalysis;
using SAF.Messaging.Contracts;
using SAF.Messaging.WireFormat;
using StackExchange.Redis;

/// <summary>
/// Writes each message in the oldest format that can carry it, so older nodes can read as much as possible.
/// </summary>
internal sealed class RedisMessageWriter : IWireMessageWriter<RedisValue>
{
    private readonly IReadOnlyList<IRedisEnvelopeFormat> _formats;
    private readonly IRedisBinaryFrame _binaryFrame;

    public RedisMessageWriter(IEnumerable<IRedisEnvelopeFormat> formats, IRedisBinaryFrame binaryFrame)
    {
        _formats = formats.OrderBy(f => f.MajorVersions.Max()).ToList();
        _binaryFrame = binaryFrame;
        Formats = _formats.Aggregate(MessageFormats.None, (all, format) => all | format.Format);
    }

    public MessageFormats Formats { get; }

    public bool TryWrite(Message message, out RedisValue wire, [NotNullWhen(false)] out string? dropReason)
    {
        var format = _formats.FirstOrDefault(f => f.CanWrite(message));
        if (format == null)
        {
            wire = RedisValue.Null;
            dropReason = $"Redis messaging has no wire format for a message with format {message.GetFormat()}.";
            return false;
        }

        var wireValue = format.Write(message);
        // Without a binary payload the envelope stays plain JSON, which every SAF version reads.
        wire = wireValue.BinaryPayload is null ? wireValue.Envelope : _binaryFrame.Write(wireValue.Envelope, wireValue.BinaryPayload);
        dropReason = null;
        return true;
    }
}
