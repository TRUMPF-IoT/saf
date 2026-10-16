// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

using NATS.Client.Core;

/// <summary>
/// What goes on the wire for one message: a text or a binary body and the optional SAF headers.
/// </summary>
internal readonly record struct NatsWireMessage
{
    private NatsWireMessage(string? textBody, byte[]? binaryBody, NatsHeaders? headers)
    {
        TextBody = textBody;
        BinaryBody = binaryBody;
        Headers = headers;
    }

    public string? TextBody { get; }
    public byte[]? BinaryBody { get; }
    public NatsHeaders? Headers { get; }

    public static NatsWireMessage Text(string? body, NatsHeaders? headers) => new(body, null, headers);

    public static NatsWireMessage Binary(byte[] body, NatsHeaders? headers) => new(null, body, headers);

    // NATS.Net encodes a string body as UTF-8 itself, so text needs no extra buffer.
    public ValueTask PublishAsync(INatsClient client, string subject)
        => BinaryBody != null
            ? client.PublishAsync(subject, BinaryBody, headers: Headers)
            : client.PublishAsync(subject, TextBody, headers: Headers);
}
