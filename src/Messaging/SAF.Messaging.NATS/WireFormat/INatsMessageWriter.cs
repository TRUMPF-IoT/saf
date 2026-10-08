// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

using System.Diagnostics.CodeAnalysis;
using SAF.Messaging.Contracts;

internal interface INatsMessageWriter
{
    /// <param name="dropReason">Why the message is not sent, if the result is <c>false</c>.</param>
    bool TryWrite(Message message, out NatsWireMessage wireMessage, [NotNullWhen(false)] out string? dropReason);
}
