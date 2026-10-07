// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.Tests.WireFormat;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SAF.Messaging.Nats.WireFormat;

/// <summary>
/// The production wire format composition, as the plug-in builds it.
/// </summary>
internal static class TestWireFormat
{
    public static INatsMessageWriter Writer() => new NatsMessageWriter(NatsWireFormats.All);

    public static INatsMessageReader Reader(ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;
        return new NatsMessageReader(NatsWireFormats.All, new UnknownVersionWarning(logger), logger);
    }
}
