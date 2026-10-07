// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

/// <summary>
/// The JSON envelope every SAF node since 9.x writes to a Redis channel.
/// </summary>
internal sealed class RedisEnvelope<TMessage>
{
    public string? Version { get; set; }
    public TMessage? Message { get; set; }
}

/// <summary>
/// Reads only the version of an envelope whose message part could not be read.
/// </summary>
internal sealed class RedisEnvelopeHeader
{
    public string? Version { get; set; }
}
