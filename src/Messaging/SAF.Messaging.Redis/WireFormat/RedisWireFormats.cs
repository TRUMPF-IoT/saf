// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

/// <summary>
/// The envelope formats this SAF version supports. Register a new format here.
/// </summary>
internal static class RedisWireFormats
{
    public static IReadOnlyList<IRedisEnvelopeFormat> All { get; } = [new RedisEnvelopeV2Format()];
}
