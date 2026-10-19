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
/// What a receiver needs to know of an envelope before it reads the message.
/// </summary>
/// <param name="Version">The envelope version; <c>null</c> in the oldest envelopes.</param>
internal sealed record RedisEnvelopeHeader(string? Version);
