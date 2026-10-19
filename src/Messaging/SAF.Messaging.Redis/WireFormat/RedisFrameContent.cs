// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

using System.Text;

/// <summary>
/// The content of a <see cref="RedisBinaryFrame"/>: the envelope still as UTF-8, so its header can be read without
/// decoding it, and the binary payload.
/// </summary>
internal readonly record struct RedisFrameContent(ReadOnlyMemory<byte> Envelope, byte[] BinaryPayload)
{
    public RedisWireValue ToWireValue() => new(Encoding.UTF8.GetString(Envelope.Span), BinaryPayload);
}
