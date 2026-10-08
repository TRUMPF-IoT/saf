// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

using SAF.Messaging.Contracts;

/// <summary>
/// The envelope every SAF node since 9.x writes: <c>{"version":"2.0.0","message":{...}}</c>.
/// </summary>
internal sealed class RedisEnvelopeV2Format(IRedisEnvelopeSerializer serializer) : IRedisEnvelopeFormat
{
    // V1 was declared together with V2 but never written; both share this shape.
    public IReadOnlyCollection<int> MajorVersions { get; } = [1, 2];

    // The JSON envelope carries text only.
    public bool CanWrite(Message message) => message.BinaryPayload is null;

    public RedisWireValue Write(Message message) => new(serializer.Serialize(RedisMessageVersion.V2, message));

    public Message? Read(RedisWireValue value)
        => value.BinaryPayload is null ? serializer.Deserialize(value.Envelope) : null;
}
