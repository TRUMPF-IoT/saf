// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using NATS.Client.Core;
using SAF.Messaging.Contracts;

/// <summary>
/// The body is the payload, the custom properties travel in the <see cref="NatsHeaderNames.Metadata"/> header.
/// Older nodes ignore the headers and still read the payload.
/// </summary>
internal sealed class NatsV2Format : INatsWireFormat
{
    private const string Version = "2.0.0";

    // NATS writes header values as ASCII, so every non-ASCII character has to be escaped.
    private static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Default
    };

    public int MajorVersion => 2;

    public bool CanWrite(Message message) => true;

    public NatsWireMessage Write(Message message)
        => new(message.Payload, new NatsHeaders
        {
            { NatsHeaderNames.Version, Version },
            { NatsHeaderNames.Metadata, JsonSerializer.Serialize(MessageMetadataDtoV2.FromMessage(message), MetadataJsonOptions) }
        });

    public Message? Read(string topic, string? body, NatsHeaders? headers)
    {
        if (headers == null || !headers.TryGetValue(NatsHeaderNames.Metadata, out var json))
            return new Message { Topic = topic, Payload = body };

        try
        {
            var metadata = JsonSerializer.Deserialize<MessageMetadataDtoV2>(json.ToString(), MetadataJsonOptions);
            return new Message { Topic = topic, Payload = body, CustomProperties = metadata?.ToCustomProperties() };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
