// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

/// <summary>
/// What a Redis value carries: the JSON envelope and, in a <see cref="RedisBinaryFrame"/>, the binary payload.
/// </summary>
internal readonly record struct RedisWireValue(string Envelope, byte[]? BinaryPayload = null);
