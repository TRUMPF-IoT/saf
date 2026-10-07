// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

using System.Text.Json;
using SAF.Messaging.Contracts;
using JsonSerializer = Toolbox.Serialization.JsonSerializer;

/// <summary>
/// The envelope every SAF node since 9.x writes: <c>{"version":"2.0.0","message":{...}}</c>.
/// </summary>
internal sealed class RedisEnvelopeV2Format : IRedisEnvelopeFormat
{
    // V1 was declared together with V2 but never written; both share this shape.
    public IReadOnlyCollection<int> MajorVersions { get; } = [1, 2];

    public bool CanWrite(Message message) => true;

    public string Write(Message message)
        => JsonSerializer.Serialize(new RedisEnvelope<MessageDtoV2>
        {
            Version = RedisMessageVersion.V2,
            Message = MessageDtoV2.FromMessage(message)
        });

    public Message? Read(string envelopeJson)
    {
        try
        {
            return JsonSerializer.Deserialize<RedisEnvelope<MessageDtoV2>>(envelopeJson)?.Message?.ToMessage();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
