// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using NATS.Client.Core;

/// <summary>
/// The metadata as camelCase JSON in which every character outside ASCII is escaped.
/// </summary>
internal sealed class NatsMetadataHeader : INatsMetadataHeader
{
    // NATS writes header values as ASCII, so every non-ASCII character has to be escaped.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Default
    };

    public string Write(MessageMetadataDtoV2 metadata) => JsonSerializer.Serialize(metadata, JsonOptions);

    public bool TryRead(NatsHeaders? headers, out MessageMetadataDtoV2? metadata)
    {
        metadata = null;
        if (headers == null || !headers.TryGetValue(NatsHeaderNames.Metadata, out var json)) return true;

        try
        {
            metadata = JsonSerializer.Deserialize<MessageMetadataDtoV2>(json.ToString(), JsonOptions);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
