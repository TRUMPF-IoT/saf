// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

using System.Text.Json;

/// <summary>
/// Scans the top-level properties only and stops at the message once the version is known, which SAF writes first.
/// </summary>
internal sealed class RedisEnvelopeHeaderReader : IRedisEnvelopeHeaderReader
{
    // Accepts what the Toolbox serializer accepts when it reads the envelope.
    private static readonly JsonReaderOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public RedisEnvelopeHeader? Read(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            return Scan(new Utf8JsonReader(utf8Json, Options));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static RedisEnvelopeHeader? Scan(Utf8JsonReader reader)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return null;

        string? version = null;
        bool hasVersion = false, hasMessage = false;
        while (!(hasVersion && hasMessage) && reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var name = reader.GetString();
            reader.Read();
            if (!hasVersion && IsProperty(name, "version"))
            {
                if (reader.TokenType is not (JsonTokenType.String or JsonTokenType.Null)) return null;
                version = reader.GetString();
                hasVersion = true;
            }
            else if (!hasMessage && IsProperty(name, "message") && reader.TokenType == JsonTokenType.StartObject)
            {
                hasMessage = true;
                reader.Skip();
            }
            else
            {
                reader.Skip();
            }
        }

        return hasMessage ? new RedisEnvelopeHeader(version) : null;
    }

    private static bool IsProperty(string? name, string expected) => string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);
}
