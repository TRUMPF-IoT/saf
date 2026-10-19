// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.WireFormat;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Warns once per unknown wire format version instead of once per dropped message.
/// </summary>
/// <param name="transport">Names the transport in the warning, for example <c>Redis</c>.</param>
public sealed class UnknownVersionWarning(ILogger logger, string transport) : IUnknownVersionWarning
{
    // Bounds memory when a peer sends arbitrary version strings.
    private const int MaxReportedVersions = 32;

    private readonly ConcurrentDictionary<string, byte> _reportedVersions = new();

    public void Report(string version, string topic)
    {
        if (_reportedVersions.Count >= MaxReportedVersions || !_reportedVersions.TryAdd(version, 0)) return;

        logger.LogWarning(
            "Dropped {Transport} message on {Topic}: unknown wire format version {Version}. The sender runs a newer SAF version; further messages with this version are dropped silently.",
            transport, topic, version);
    }
}
