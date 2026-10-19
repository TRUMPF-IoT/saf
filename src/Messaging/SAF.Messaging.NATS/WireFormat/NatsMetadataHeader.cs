// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using NATS.Client.Core;

/// <summary>
/// The metadata as camelCase JSON in which every character outside printable ASCII is escaped.
/// </summary>
internal sealed class NatsMetadataHeader : INatsMetadataHeader
{
    // Escapes only what JSON requires; the default encoder also escapes quotes and HTML characters as \uXXXX, which
    // tripled the header of a JSON payload. The header never ends up in HTML.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string Write(MessageMetadataDtoV2 metadata) => EscapeNonAscii(JsonSerializer.Serialize(metadata, JsonOptions));

    // NATS writes header values as ASCII and turns every other character into '?'. Outside strings, JSON is ASCII.
    private static string EscapeNonAscii(string json)
    {
        var first = json.AsSpan().IndexOfAnyExceptInRange(' ', '~');
        if (first < 0) return json;

        var escaped = new StringBuilder(json.Length + 32).Append(json, 0, first);
        foreach (var c in json.AsSpan(first))
        {
            if (c is >= ' ' and <= '~') escaped.Append(c);
            // The interpolation handler formats into the builder, without a string per character.
            else escaped.Append($@"\u{(int)c:X4}");
        }
        return escaped.ToString();
    }

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
