// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

using NATS.Client.Core;
using SAF.Messaging.Contracts;

/// <summary>
/// The body is the textual payload, all other message fields travel in the <see cref="NatsHeaderNames.Metadata"/>
/// header. Older nodes ignore the headers and still read the payload.
/// </summary>
internal sealed class NatsV2Format(INatsMetadataHeader metadataHeader) : INatsWireFormat
{
    private const string Version = "2.0.0";

    public int MajorVersion => 2;

    public bool CanWrite(Message message) => message.BinaryPayload is null;

    public NatsWireMessage Write(Message message)
        => NatsWireMessage.Text(message.Payload, new NatsHeaders
        {
            { NatsHeaderNames.Version, Version },
            { NatsHeaderNames.Metadata, metadataHeader.Write(MessageMetadataDtoV2.FromMessage(message)) }
        });

    public Message? Read(string topic, NatsBody body, NatsHeaders? headers)
    {
        if (!metadataHeader.TryRead(headers, out var metadata)) return null;

        var payload = body.ReadText();
        return metadata?.ToMessage(topic, payload) ?? new Message { Topic = topic, Payload = payload };
    }
}
