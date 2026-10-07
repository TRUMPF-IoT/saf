// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

using SAF.Messaging.Contracts;

/// <summary>
/// Writes each message in the oldest format that can carry it, so older nodes can read as much as possible.
/// </summary>
internal sealed class RedisMessageWriter : IRedisMessageWriter
{
    private readonly IReadOnlyList<IRedisEnvelopeFormat> _formats;

    public RedisMessageWriter(IEnumerable<IRedisEnvelopeFormat> formats)
    {
        _formats = formats.OrderBy(f => f.MajorVersions.Max()).ToList();
    }

    public string Write(Message message)
        => (_formats.FirstOrDefault(f => f.CanWrite(message))
            ?? throw new InvalidOperationException($"No Redis wire format can write the message on {message.Topic}."))
            .Write(message);
}
