// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

using System.Text.Json;
using SAF.Messaging.Contracts;
using JsonSerializer = Toolbox.Serialization.JsonSerializer;

/// <summary>
/// The envelope <c>{"version":"…","message":{…}}</c> in the camelCase JSON of the Toolbox serializer.
/// </summary>
internal sealed class RedisEnvelopeSerializer : IRedisEnvelopeSerializer
{
    public string Serialize(string version, Message message)
        => JsonSerializer.Serialize(new RedisEnvelope<MessageDtoV2> { Version = version, Message = MessageDtoV2.FromMessage(message) });

    public Message? Deserialize(string envelope)
    {
        try
        {
            return JsonSerializer.Deserialize<RedisEnvelope<MessageDtoV2>>(envelope)?.Message?.ToMessage();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
