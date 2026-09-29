// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace TestUtilities.WireFormat;

using SAF.Messaging.Contracts;

/// <summary>
/// The canonical message set every transport records its wire-format golden samples for.
/// Changing an entry invalidates the recorded samples of all transports - that is intended.
/// </summary>
public static class WireFormatReferenceMessages
{
    public const string TopicOnly = "topic-only";
    public const string TextPayload = "text-payload";
    public const string CustomProperties = "custom-properties";
    public const string EmptyPayload = "empty-payload";
    public const string UnicodeAndEscapes = "unicode-and-escapes";
    public const string NullPropertyValue = "null-property-value";

    /// <summary>
    /// Covers quote, backslash, slash, control characters, non-ASCII and a surrogate pair.
    /// </summary>
    public const string UnicodeAndEscapesPayload =
        "\"quote\" \\back\\slash / slash\r\n\ttab äöüß 日本語 😀";

    public static IReadOnlyList<string> Ids =>
    [
        TopicOnly,
        TextPayload,
        CustomProperties,
        EmptyPayload,
        UnicodeAndEscapes,
        NullPropertyValue
    ];

    /// <summary>
    /// Creates a fresh instance so a test can never observe mutations made by another test.
    /// </summary>
    public static Message Create(string id) => id switch
    {
        TopicOnly => new Message
        {
            Topic = "saf/wire/topic-only"
        },
        TextPayload => new Message
        {
            Topic = "saf/wire/text",
            Payload = """{"value":42,"name":"sensor"}"""
        },
        CustomProperties => new Message
        {
            Topic = "saf/wire/props",
            Payload = """{"value":42}""",
            CustomProperties =
            [
                new MessageCustomProperty { Name = "replyTo", Value = "saf/wire/reply" },
                new MessageCustomProperty { Name = "correlationId", Value = "c3f1a0" }
            ]
        },
        EmptyPayload => new Message
        {
            Topic = "saf/wire/empty",
            Payload = ""
        },
        UnicodeAndEscapes => new Message
        {
            Topic = "saf/wire/unicode",
            Payload = UnicodeAndEscapesPayload
        },
        NullPropertyValue => new Message
        {
            Topic = "saf/wire/null-prop",
            Payload = "plain text",
            CustomProperties =
            [
                new MessageCustomProperty { Name = "flag", Value = null }
            ]
        },
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown reference message id.")
    };
}
