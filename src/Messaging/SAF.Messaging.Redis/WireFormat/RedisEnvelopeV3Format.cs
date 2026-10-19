// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

using SAF.Messaging.Contracts;

/// <summary>
/// A message with a binary payload: the envelope <c>{"version":"3.0.0","message":{...}}</c> and the binary payload,
/// which travel together in a binary frame. Nodes before SAF 11 read that frame as corrupt text.
/// Outside a frame, as code writes it that builds envelopes with <see cref="RedisMessageVersion.Latest"/>, the
/// envelope is a text message.
/// </summary>
internal sealed class RedisEnvelopeV3Format(IRedisEnvelopeSerializer serializer) : IRedisEnvelopeFormat
{
    public IReadOnlyCollection<int> MajorVersions { get; } = [3];

    public MessageFormats Format => MessageFormats.Binary;

    public bool CanWrite(Message message) => message.BinaryPayload is not null;

    public RedisWireValue Write(Message message)
        => new(serializer.Serialize(RedisMessageVersion.V3, message), message.BinaryPayload);

    public Message? Read(RedisWireValue value)
    {
        var message = serializer.Deserialize(value.Envelope);
        if (message != null) message.BinaryPayload = value.BinaryPayload;
        return message;
    }
}
