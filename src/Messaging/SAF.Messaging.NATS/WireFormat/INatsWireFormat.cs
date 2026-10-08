// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

using NATS.Client.Core;
using SAF.Messaging.Contracts;

/// <summary>
/// One major version of the NATS wire format. A new version is added as a new implementation.
/// </summary>
internal interface INatsWireFormat
{
    int MajorVersion { get; }

    bool CanWrite(Message message);

    NatsWireMessage Write(Message message);

    /// <returns><c>null</c> if the SAF headers cannot be read.</returns>
    Message? Read(string topic, NatsBody body, NatsHeaders? headers);
}
