// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

using NATS.Client.Core;
using SAF.Messaging.Contracts;

/// <summary>
/// The header-less format of every SAF node up to 11.0.0-alpha.9: the body is the payload, nothing else.
/// Also works with NATS servers before 2.2.
/// </summary>
internal sealed class NatsV1Format : INatsWireFormat
{
    public int MajorVersion => 1;

    public bool CanWrite(Message message)
        => message is { BinaryPayload: null, AcceptedReplyFormats: null, CustomProperties: null };

    public NatsWireMessage Write(Message message) => new(message.Payload, null);

    public Message Read(string topic, string? body, NatsHeaders? headers) => new() { Topic = topic, Payload = body };
}
