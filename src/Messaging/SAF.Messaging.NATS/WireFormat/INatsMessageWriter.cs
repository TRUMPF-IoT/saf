// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

using SAF.Messaging.Contracts;

internal interface INatsMessageWriter
{
    /// <returns><c>false</c> if no format can carry the message.</returns>
    bool TryWrite(Message message, out NatsWireMessage wireMessage);
}
