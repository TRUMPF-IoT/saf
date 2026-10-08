// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

using System.Diagnostics.CodeAnalysis;
using SAF.Messaging.Contracts;
using StackExchange.Redis;

internal interface IRedisMessageWriter
{
    /// <param name="dropReason">Why the message is not sent, if the result is <c>false</c>.</param>
    bool TryWrite(Message message, out RedisValue value, [NotNullWhen(false)] out string? dropReason);
}
