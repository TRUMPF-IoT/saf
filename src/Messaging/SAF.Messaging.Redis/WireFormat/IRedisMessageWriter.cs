// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

using SAF.Messaging.Contracts;

internal interface IRedisMessageWriter
{
    string Write(Message message);
}
