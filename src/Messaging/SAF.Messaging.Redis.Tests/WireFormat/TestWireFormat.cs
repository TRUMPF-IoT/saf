// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.Tests.WireFormat;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SAF.Messaging.Redis.WireFormat;
using SAF.Messaging.WireFormat;
using StackExchange.Redis;

/// <summary>
/// The production wire format composition, as the plug-in builds it.
/// </summary>
internal static class TestWireFormat
{
    public static IWireMessageWriter<RedisValue> Writer(bool enableBinaryPayloads = true)
        => ServiceCollectionExtensions.CreateWriter(new RedisConfiguration { EnableBinaryPayloads = enableBinaryPayloads });

    public static IRedisMessageReader Reader(ILogger? logger = null)
        => ServiceCollectionExtensions.CreateReader(logger ?? NullLogger.Instance);
}
