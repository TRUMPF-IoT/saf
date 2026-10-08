// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SAF.Messaging.Contracts;
using StackExchange.Redis;
using JsonSerializer = Toolbox.Serialization.JsonSerializer;

/// <summary>
/// Reads the envelope version and hands the value to the format registered for its major version.
/// A binary frame is unpacked first.
/// </summary>
internal sealed class RedisMessageReader : IRedisMessageReader
{
    private readonly IReadOnlyDictionary<int, IRedisEnvelopeFormat> _formatsByMajorVersion;
    private readonly IRedisBinaryFrame _binaryFrame;
    private readonly UnknownVersionWarning _unknownVersionWarning;
    private readonly ILogger _logger;

    public RedisMessageReader(IEnumerable<IRedisEnvelopeFormat> formats, IRedisBinaryFrame binaryFrame,
        UnknownVersionWarning unknownVersionWarning, ILogger logger)
    {
        _formatsByMajorVersion = formats
            .SelectMany(format => format.MajorVersions.Select(major => (major, format)))
            .ToDictionary(entry => entry.major, entry => entry.format);
        _binaryFrame = binaryFrame;
        _unknownVersionWarning = unknownVersionWarning;
        _logger = logger;
    }

    public Message? Read(string channel, RedisValue value)
        => _binaryFrame.IsFrame(value) ? ReadFrame(channel, value) : ReadText(channel, value.ToString());

    private Message? ReadText(string channel, string value)
    {
        // Not an envelope: someone else publishes plain values on this channel.
        if (!TryReadVersion(value, out var version)) return Raw(channel, value);
        if (!TryGetFormat(version, channel, out var format)) return null;

        return format.Read(new RedisWireValue(value)) ?? Raw(channel, value);
    }

    private Message? ReadFrame(string channel, RedisValue frame)
    {
        if (_binaryFrame.GetNewerLayoutVersion(frame) is { } layoutVersion)
        {
            _unknownVersionWarning.Report($"binary frame {layoutVersion}", channel);
            return null;
        }

        if (_binaryFrame.Read(frame) is not { } value || !TryReadVersion(value.Envelope, out var version))
            return DropUnreadableFrame(channel);
        if (!TryGetFormat(version, channel, out var format)) return null;

        return format.Read(value) ?? DropUnreadableFrame(channel);
    }

    private bool TryGetFormat(string? version, string channel, [NotNullWhen(true)] out IRedisEnvelopeFormat? format)
    {
        format = null;
        if (WireVersion.TryGetMajor(version, out var major) && _formatsByMajorVersion.TryGetValue(major, out format))
            return true;

        _unknownVersionWarning.Report(version!, channel);
        return false;
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

    private Message? DropUnreadableFrame(string channel)
    {
        _logger.LogWarning("Dropped Redis message on {Channel}: its binary frame cannot be read.", channel);
        return null;
    }

    private static Message Raw(string channel, string value) => new() { Topic = channel, Payload = value };
}
