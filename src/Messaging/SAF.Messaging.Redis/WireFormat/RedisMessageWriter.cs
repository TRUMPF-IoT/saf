// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

using System.Diagnostics.CodeAnalysis;
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

    public bool TryWrite(Message message, [NotNullWhen(true)] out string? value)
    {
        value = _formats.FirstOrDefault(f => f.CanWrite(message))?.Write(message);
        return value != null;
    }
}
