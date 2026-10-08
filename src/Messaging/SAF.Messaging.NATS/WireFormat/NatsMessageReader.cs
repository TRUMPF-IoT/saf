// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using SAF.Messaging.Contracts;

/// <summary>
/// Reads the <see cref="NatsHeaderNames.Version"/> header and hands the message to the format registered for its
/// major version. Without that header the sender is an older SAF node or a foreign publisher.
/// </summary>
internal sealed class NatsMessageReader : INatsMessageReader
{
    private readonly IReadOnlyDictionary<int, INatsWireFormat> _formatsByMajorVersion;
    private readonly UnknownVersionWarning _unknownVersionWarning;
    private readonly ILogger _logger;

    public NatsMessageReader(IEnumerable<INatsWireFormat> formats, UnknownVersionWarning unknownVersionWarning, ILogger logger)
    {
        _formatsByMajorVersion = formats.ToDictionary(f => f.MajorVersion);
        _unknownVersionWarning = unknownVersionWarning;
        _logger = logger;
    }

    public Message? Read(string topic, NatsBody body, NatsHeaders? headers)
    {
        var version = headers != null && headers.TryGetValue(NatsHeaderNames.Version, out var values)
            ? values.ToString()
            : null;

        if (!WireVersion.TryGetMajor(version, out var major) || !_formatsByMajorVersion.TryGetValue(major, out var format))
        {
            _unknownVersionWarning.Report(version!, topic);
            return null;
        }

        var message = format.Read(topic, body, headers);
        if (message == null)
            _logger.LogWarning("Dropped NATS message on {Topic}: its SAF headers cannot be read.", topic);

        return message;
    }
}
