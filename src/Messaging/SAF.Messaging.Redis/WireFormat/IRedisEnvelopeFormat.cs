// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

using SAF.Messaging.Contracts;

/// <summary>
/// One shape of the Redis envelope. A new shape is added as a new implementation.
/// </summary>
internal interface IRedisEnvelopeFormat
{
    /// <summary>
    /// The envelope major versions this format reads.
    /// </summary>
    IReadOnlyCollection<int> MajorVersions { get; }

    bool CanWrite(Message message);

    RedisWireValue Write(Message message);

    /// <returns><c>null</c> if the value carries no readable message.</returns>
    Message? Read(RedisWireValue value);
}
