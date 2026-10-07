// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

using System.Diagnostics.CodeAnalysis;
using SAF.Messaging.Contracts;

internal interface IRedisMessageWriter
{
    /// <returns><c>false</c> if no format can carry the message.</returns>
    bool TryWrite(Message message, [NotNullWhen(true)] out string? value);
}
