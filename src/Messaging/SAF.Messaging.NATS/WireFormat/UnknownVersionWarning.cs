// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Warns once per unknown wire format version instead of once per dropped message.
/// </summary>
internal sealed class UnknownVersionWarning(ILogger logger)
{
    // Bounds memory when a peer sends arbitrary version strings.
    private const int MaxReportedVersions = 32;

    private readonly ConcurrentDictionary<string, byte> _reportedVersions = new();

    public void Report(string version, string topic)
    {
        if (_reportedVersions.Count >= MaxReportedVersions || !_reportedVersions.TryAdd(version, 0)) return;

        logger.LogWarning(
            "Dropped NATS message on {Topic}: unknown wire format version {Version}. The sender runs a newer SAF version; further messages with this version are dropped silently.",
            topic, version);
    }
}
