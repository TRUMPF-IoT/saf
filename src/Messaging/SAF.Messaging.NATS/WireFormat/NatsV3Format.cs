// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

using NATS.Client.Core;
using SAF.Messaging.Contracts;

/// <summary>
/// The body is the binary payload. The text payload and all other message fields travel in the
/// <see cref="NatsHeaderNames.Metadata"/> header, which is left out if none of them is set. Nodes before SAF 11 ignore
/// the headers and read the body as corrupt text.
/// </summary>
internal sealed class NatsV3Format(INatsMetadataHeader metadataHeader) : INatsWireFormat
{
    private const string Version = "3.0.0";

    public int MajorVersion => 3;

    public bool CanWrite(Message message) => message.BinaryPayload is not null;

    public NatsWireMessage Write(Message message)
    {
        var headers = new NatsHeaders { { NatsHeaderNames.Version, Version } };
        var metadata = MessageMetadataDtoV2.FromMessage(message);
        metadata.Payload = message.Payload;
        if (!metadata.IsEmpty()) headers.Add(NatsHeaderNames.Metadata, metadataHeader.Write(metadata));

        return NatsWireMessage.Binary(message.BinaryPayload!, headers);
    }

    public Message? Read(string topic, NatsBody body, NatsHeaders? headers)
    {
        if (!metadataHeader.TryRead(headers, out var metadata)) return null;

        var message = metadata?.ToMessage(topic, metadata.Payload) ?? new Message { Topic = topic };
        // An empty binary payload arrives as an empty body.
        message.BinaryPayload = body.ToArray();
        return message;
    }
}
