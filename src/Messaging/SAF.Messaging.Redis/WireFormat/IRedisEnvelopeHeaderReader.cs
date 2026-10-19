// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

/// <summary>
/// Tells a SAF envelope from other values on a channel without reading its message.
/// </summary>
internal interface IRedisEnvelopeHeaderReader
{
    /// <returns>The header, or <c>null</c> if the value is no JSON object with a <c>message</c> object.</returns>
    RedisEnvelopeHeader? Read(ReadOnlySpan<byte> utf8Json);
}
