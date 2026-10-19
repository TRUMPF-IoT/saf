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

        var scan = new HeaderScan();
        while (!scan.IsComplete && reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (!scan.ReadProperty(ref reader)) return null;
        }

        return scan.HasMessage ? new RedisEnvelopeHeader(scan.Version) : null;
    }

    private ref struct HeaderScan
    {
        public string? Version { get; private set; }
        public bool HasMessage { get; private set; }
        private bool _hasVersion;

        public readonly bool IsComplete => _hasVersion && HasMessage;

        // Returns false if the value is no envelope.
        public bool ReadProperty(ref Utf8JsonReader reader)
        {
            var name = reader.GetString();
            reader.Read();
            if (!_hasVersion && IsProperty(name, "version"))
                return ReadVersion(ref reader);

            if (IsProperty(name, "message") && reader.TokenType == JsonTokenType.StartObject)
            {
                HasMessage = true;
                // The format reads the message; scanning it here too would read a large payload twice.
                if (_hasVersion) return true;
            }

            reader.Skip();
            return true;
        }

        private bool ReadVersion(ref Utf8JsonReader reader)
        {
            if (reader.TokenType is not (JsonTokenType.String or JsonTokenType.Null)) return false;
            Version = reader.GetString();
            _hasVersion = true;
            return true;
        }

        private static bool IsProperty(string? name, string expected) => string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);
    }
}
