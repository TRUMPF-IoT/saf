// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

using NATS.Client.Core;
using SAF.Messaging.Contracts;

internal interface INatsMessageReader
{
    /// <returns><c>null</c> if the message has to be dropped.</returns>
    Message? Read(string topic, string? body, NatsHeaders? headers);
}
