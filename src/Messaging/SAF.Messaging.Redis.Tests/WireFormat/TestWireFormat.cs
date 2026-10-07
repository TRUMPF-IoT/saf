// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.Tests.WireFormat;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SAF.Messaging.Redis.WireFormat;

/// <summary>
/// The production wire format composition, as the plug-in builds it.
/// </summary>
internal static class TestWireFormat
{
    public static IRedisMessageWriter Writer() => new RedisMessageWriter(RedisWireFormats.All);

    public static IRedisMessageReader Reader(ILogger? logger = null)
        => new RedisMessageReader(RedisWireFormats.All, new UnknownVersionWarning(logger ?? NullLogger.Instance));
}
