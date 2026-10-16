// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Contracts;

/// <summary>
/// A pub/sub message.
/// </summary>
/// <remarks>
/// Do not change a message once it is published, neither as publisher nor in a handler:
/// transports may hand the same instance, including its payload arrays, to several handlers.
/// </remarks>
public class Message
{
    /// <summary>
    /// Gets or sets the topic where the message is published.
    /// </summary>
    public string Topic { get; set; } = default!;

    /// <summary>
    /// Gets or sets the textual payload of the message (usually JSON).
    /// </summary>
    public string? Payload { get; set; }

    /// <summary>
    /// Gets or sets the binary payload of the message.
    /// Can be combined with <see cref="Payload"/> to carry metadata and bulk data in one message.
    /// </summary>
    public byte[]? BinaryPayload { get; set; }

    /// <summary>
    /// Gets or sets the topic a reply to this message is expected on. <c>null</c> if the sender expects no reply.
    /// </summary>
    /// <remarks>
    /// Requests from SAF 10.x or older carry the reply topic only in their payload, see <see cref="MessageRequestBase.ReplyTo"/>.
    /// </remarks>
    public string? ReplyTo { get; set; }

    /// <summary>
    /// Gets or sets the payload formats the sender accepts for a reply.
    /// <c>null</c> means the sender stated no capability and understands text only.
    /// </summary>
    public MessageFormats? AcceptedReplyFormats { get; set; }

    /// <summary>
    /// Gets or sets custom properties of the message.
    /// Custom properties are string key-value pairs used to add metadata to SAF messages.
    /// </summary>
    public List<MessageCustomProperty>? CustomProperties { get; set; }

    /// <summary>
    /// Gets the payload formats present in this message.
    /// </summary>
    public MessageFormats GetFormat()
        => (Payload is not null ? MessageFormats.Text : MessageFormats.None)
           | (BinaryPayload is not null ? MessageFormats.Binary : MessageFormats.None);

    /// <summary>
    /// Gets whether a reply to this message may carry all of the given payload formats.
    /// Without <see cref="AcceptedReplyFormats"/>, only text is accepted.
    /// </summary>
    public bool AcceptsReplyFormat(MessageFormats formats)
        => ((AcceptedReplyFormats ?? MessageFormats.Text) & formats) == formats;
}

public class MessageCustomProperty
{
    public string Name { get; set; } = default!;
    public string? Value { get; set; }
}
