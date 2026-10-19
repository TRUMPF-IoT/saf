// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

using System.Buffers.Binary;
using System.Text;
using StackExchange.Redis;

/// <summary>
/// Layout 1: <c>'S' 'A' 'F' 'B' | layout version (1 byte) | envelope length (uint32, little-endian) | envelope (UTF-8 JSON) | binary payload</c>.
/// A JSON envelope never starts with the magic, so both share one channel. A layout version is a control character,
/// which text of other applications rarely has after the magic.
/// </summary>
internal sealed class RedisBinaryFrame : IRedisBinaryFrame
{
    public const byte LayoutVersion = 1;

    // Layouts 1 to 31; a printable byte after the magic is text of another application.
    private const byte MaxLayoutVersion = 0x1F;

    private const int LayoutVersionOffset = 4;
    private const int EnvelopeLengthOffset = 5;
    private const int EnvelopeOffset = 9;

    private static readonly RedisValue Magic = "SAFB"u8.ToArray();

    public bool IsFrame(RedisValue value)
    {
        if (!value.StartsWith(Magic)) return false;

        var bytes = ((ReadOnlyMemory<byte>)value).Span;
        return bytes.Length > LayoutVersionOffset && bytes[LayoutVersionOffset] <= MaxLayoutVersion;
    }

    public RedisValue Write(string envelope, byte[] binaryPayload)
    {
        var envelopeLength = Encoding.UTF8.GetByteCount(envelope);
        var frame = new byte[EnvelopeOffset + envelopeLength + binaryPayload.Length];
        "SAFB"u8.CopyTo(frame);
        frame[LayoutVersionOffset] = LayoutVersion;
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(EnvelopeLengthOffset), (uint)envelopeLength);
        Encoding.UTF8.GetBytes(envelope, frame.AsSpan(EnvelopeOffset));
        binaryPayload.CopyTo(frame.AsSpan(EnvelopeOffset + envelopeLength));
        return frame;
    }

    public byte? GetNewerLayoutVersion(RedisValue frame)
    {
        var bytes = ((ReadOnlyMemory<byte>)frame).Span;
        return bytes.Length > LayoutVersionOffset && bytes[LayoutVersionOffset] > LayoutVersion ? bytes[LayoutVersionOffset] : null;
    }

    public RedisFrameContent? Read(RedisValue frame)
    {
        var bytes = (ReadOnlyMemory<byte>)frame;
        if (bytes.Length < EnvelopeOffset || bytes.Span[LayoutVersionOffset] != LayoutVersion) return null;

        var envelopeLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Span[EnvelopeLengthOffset..]);
        if (envelopeLength > (uint)(bytes.Length - EnvelopeOffset)) return null;

        var binaryOffset = EnvelopeOffset + (int)envelopeLength;
        return new RedisFrameContent(bytes[EnvelopeOffset..binaryOffset], bytes[binaryOffset..].ToArray());
    }
}
