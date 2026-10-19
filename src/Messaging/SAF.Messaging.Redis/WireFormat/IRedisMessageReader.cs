// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

using SAF.Messaging.Contracts;
using StackExchange.Redis;

internal interface IRedisMessageReader
{
    /// <returns><c>null</c> if the value has to be dropped.</returns>
    Message? Read(string channel, RedisValue value);
}
