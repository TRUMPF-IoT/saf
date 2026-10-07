// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

using System.Text.Json;
using SAF.Messaging.Contracts;
using JsonSerializer = Toolbox.Serialization.JsonSerializer;

/// <summary>
/// Reads the envelope version and hands the value to the format registered for its major version.
/// </summary>
internal sealed class RedisMessageReader : IRedisMessageReader
{
    private readonly IReadOnlyDictionary<int, IRedisEnvelopeFormat> _formatsByMajorVersion;
    private readonly UnknownVersionWarning _unknownVersionWarning;

    public RedisMessageReader(IEnumerable<IRedisEnvelopeFormat> formats, UnknownVersionWarning unknownVersionWarning)
    {
        _formatsByMajorVersion = formats
            .SelectMany(format => format.MajorVersions.Select(major => (major, format)))
            .ToDictionary(entry => entry.major, entry => entry.format);
        _unknownVersionWarning = unknownVersionWarning;
    }

    public Message? Read(string channel, string value)
    {
        // Not an envelope: someone else publishes plain values on this channel.
        if (!TryReadVersion(value, out var version)) return Raw(channel, value);

        if (!WireVersion.TryGetMajor(version, out var major) || !_formatsByMajorVersion.TryGetValue(major, out var format))
        {
            _unknownVersionWarning.Report(version!, channel);
            return null;
        }

        return format.Read(value) ?? Raw(channel, value);
    }

    private static bool TryReadVersion(string value, out string? version)
    {
        try
        {
            var header = JsonSerializer.Deserialize<RedisEnvelopeHeader>(value);
            version = header?.Version;
            return header != null;
        }
        catch (JsonException)
        {
            version = null;
            return false;
        }
    }

    private static Message Raw(string channel, string value) => new() { Topic = channel, Payload = value };
}
