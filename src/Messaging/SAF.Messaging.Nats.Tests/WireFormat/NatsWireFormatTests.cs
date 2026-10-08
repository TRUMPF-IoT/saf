// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.Tests.WireFormat;

using NATS.Client.Core;
using SAF.Messaging.Contracts;
using SAF.Messaging.Nats.WireFormat;
using NSubstitute;
using Xunit;
using static TestWireFormat;

public class NatsWireFormatTests
{
    private readonly NatsV1Format _v1 = new();
    private readonly NatsV2Format _v2 = new(new NatsMetadataHeader());
    private readonly NatsV3Format _v3 = new(new NatsMetadataHeader());

    public static TheoryData<Message> MessagesBeyondV1 =>
    [
        new Message { Topic = "t", CustomProperties = [] },
        new Message { Topic = "t", AcceptedReplyFormats = MessageFormats.None },
        new Message { Topic = "t", BinaryPayload = [] }
    ];

    [Fact]
    public void V1_WritesAMessageWithPayloadOnly()
        => Assert.True(_v1.CanWrite(new Message { Topic = "t", Payload = "p" }));

    [Theory]
    [MemberData(nameof(MessagesBeyondV1))]
    public void V1_DoesNotWriteAnythingBeyondThePayload(Message message)
        => Assert.False(_v1.CanWrite(message));

    [Fact]
    public void V2_DoesNotWriteBinaryPayloads()
    {
        Assert.True(_v2.CanWrite(new Message { Topic = "t", Payload = "p", AcceptedReplyFormats = MessageFormats.Text }));
        Assert.False(_v2.CanWrite(new Message { Topic = "t", Payload = "p", BinaryPayload = [] }));
    }

    [Fact]
    public void V2_RoundTripsAcceptedReplyFormats()
    {
        var message = new Message { Topic = "t", Payload = "p", AcceptedReplyFormats = MessageFormats.Binary };

        var wire = _v2.Write(message);
        var read = _v2.Read("t", Utf8(wire.TextBody), wire.Headers)!;

        Assert.Equal("""{"acceptedReplyFormats":2}""", wire.Headers![NatsHeaderNames.Metadata].ToString());
        Assert.Equal(MessageFormats.Binary, read.AcceptedReplyFormats);
        Assert.Null(read.CustomProperties);
    }

    /// <summary>
    /// A newer node may accept reply formats this node does not know yet.
    /// </summary>
    [Fact]
    public void V2_KeepsUnknownReplyFormatFlags()
    {
        var headers = new NatsHeaders { { NatsHeaderNames.Metadata, """{"acceptedReplyFormats":7}""" } };

        Assert.Equal((MessageFormats)7, _v2.Read("t", Utf8("p"), headers)!.AcceptedReplyFormats);
    }

    [Fact]
    public void V2_ReadsNullMetadataAsAPlainMessage()
    {
        var message = _v2.Read("t", Utf8("p"), new NatsHeaders { { NatsHeaderNames.Metadata, "null" } })!;

        Assert.Equal("p", message.Payload);
        Assert.Null(message.CustomProperties);
    }

    [Fact]
    public void V1_WritesThePayloadWithoutHeaders()
        => Assert.Equal(NatsWireMessage.Text("p", null), _v1.Write(new Message { Topic = "t", Payload = "p" }));

    [Fact]
    public void V1_ReadsThePayloadAndIgnoresHeaders()
    {
        var message = _v1.Read("t", Utf8("p"), new NatsHeaders { { NatsHeaderNames.Metadata, "{}" } });

        Assert.Equal("t", message.Topic);
        Assert.Equal("p", message.Payload);
        Assert.Null(message.CustomProperties);
    }

    [Fact]
    public void V2_KeepsAnEmptyPropertyList()
    {
        var wire = _v2.Write(new Message { Topic = "t", CustomProperties = [] });

        Assert.Equal("""{"customProperties":[]}""", wire.Headers![NatsHeaderNames.Metadata].ToString());
        Assert.Empty(_v2.Read("t", Utf8(null), wire.Headers)!.CustomProperties!);
    }

    /// <summary>
    /// NATS writes header values as ASCII and turns every other character into '?'.
    /// </summary>
    [Fact]
    public void V2_EscapesEverythingOutsideAscii()
    {
        const string value = "äöü 日本 \U0001F600 \r\n\t \"q\"";
        var message = new Message { Topic = "t", CustomProperties = [new MessageCustomProperty { Name = "ü", Value = value }] };

        var wire = _v2.Write(message);
        var read = _v2.Read("t", Utf8(wire.TextBody), wire.Headers)!;

        Assert.All(wire.Headers![NatsHeaderNames.Metadata].ToString(), c => Assert.InRange(c, ' ', '~'));
        Assert.Equal("ü", read.CustomProperties![0].Name);
        Assert.Equal(value, read.CustomProperties[0].Value);
    }

    [Fact]
    public void V2_ReadsAMessageWithoutMetadata()
    {
        var message = _v2.Read("t", Utf8("p"), new NatsHeaders { { NatsHeaderNames.Version, "2.0.0" } })!;

        Assert.Equal("p", message.Payload);
        Assert.Null(message.CustomProperties);
    }

    [Fact]
    public void V2_IgnoresUnknownMetadataFields()
    {
        var headers = new NatsHeaders { { NatsHeaderNames.Metadata, """{"customProperties":[{"name":"n","value":"v"}],"futureField":1}""" } };

        Assert.Equal("v", Assert.Single(_v2.Read("t", Utf8("p"), headers)!.CustomProperties!).Value);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{"customProperties":"x"}""")]
    public void V2_ReturnsNull_ForUnreadableMetadata(string metadata)
        => Assert.Null(_v2.Read("t", Utf8("p"), new NatsHeaders { { NatsHeaderNames.Metadata, metadata } }));

    [Fact]
    public void V2_IgnoresAPayloadInTheMetadata()
    {
        var headers = new NatsHeaders { { NatsHeaderNames.Metadata, """{"payload":"meta"}""" } };

        Assert.Equal("body", _v2.Read("t", Utf8("body"), headers)!.Payload);
    }

    [Fact]
    public void V3_WritesBinaryPayloadsOnly()
    {
        Assert.True(_v3.CanWrite(new Message { Topic = "t", BinaryPayload = [] }));
        Assert.False(_v3.CanWrite(new Message { Topic = "t", Payload = "p" }));
    }

    [Fact]
    public void V3_SendsTheBinaryPayloadAsBodyAndOnlyTheVersion_WithoutOtherFields()
    {
        byte[] binaryPayload = [1, 2];

        var wire = _v3.Write(new Message { Topic = "t", BinaryPayload = binaryPayload });

        Assert.Same(binaryPayload, wire.BinaryBody);
        Assert.Null(wire.TextBody);
        var header = Assert.Single(wire.Headers!);
        Assert.Equal((NatsHeaderNames.Version, "3.0.0"), (header.Key, header.Value.ToString()));
    }

    [Fact]
    public void V3_CarriesTheTextPayloadAndTheOtherFieldsInTheMetadata()
    {
        var message = new Message
        {
            Topic = "t",
            Payload = "",
            BinaryPayload = [0, 255],
            AcceptedReplyFormats = MessageFormats.Binary,
            CustomProperties = [new MessageCustomProperty { Name = "n", Value = "v" }]
        };

        var wire = _v3.Write(message);
        var read = _v3.Read("t", new NatsBody(wire.BinaryBody), wire.Headers)!;

        Assert.Equal("""{"payload":"","acceptedReplyFormats":2,"customProperties":[{"name":"n","value":"v"}]}""",
            wire.Headers![NatsHeaderNames.Metadata].ToString());
        Assert.Equal("", read.Payload);
        Assert.Equal(message.BinaryPayload, read.BinaryPayload);
        Assert.Equal(MessageFormats.Binary, read.AcceptedReplyFormats);
        Assert.Equal("v", Assert.Single(read.CustomProperties!).Value);
    }

    /// <summary>
    /// The body is a pooled buffer that NATS reuses after the callback.
    /// </summary>
    [Fact]
    public void V3_CopiesTheBody()
    {
        byte[] body = [1, 2, 3];

        var message = _v3.Read("t", new NatsBody(body), null)!;

        Assert.Equal(body, message.BinaryPayload);
        Assert.NotSame(body, message.BinaryPayload);
    }

    [Fact]
    public void V3_ReadsAnEmptyBodyAsAnEmptyBinaryPayload()
    {
        var message = _v3.Read("t", default, new NatsHeaders { { NatsHeaderNames.Version, "3.0.0" } })!;

        Assert.Empty(message.BinaryPayload!);
        Assert.Null(message.Payload);
        Assert.Null(message.CustomProperties);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{"payload":1}""")]
    public void V3_ReturnsNull_ForUnreadableMetadata(string metadata)
        => Assert.Null(_v3.Read("t", new NatsBody(new byte[] { 1 }), new NatsHeaders { { NatsHeaderNames.Metadata, metadata } }));

    [Fact]
    public void Body_ReadsAnEmptyBodyAsNullText()
        => Assert.Null(default(NatsBody).ReadText());

    /// <summary>
    /// The formats leave the metadata header to the injected implementation.
    /// </summary>
    [Fact]
    public void Formats_UseTheInjectedMetadataHeader()
    {
        var metadataHeader = Substitute.For<INatsMetadataHeader>();
        metadataHeader.Write(Arg.Any<MessageMetadataDtoV2>()).Returns("written");
        metadataHeader.TryRead(Arg.Any<NatsHeaders?>(), out Arg.Any<MessageMetadataDtoV2?>())
            .Returns(ci =>
            {
                ci[1] = new MessageMetadataDtoV2 { Payload = "from header" };
                return true;
            });
        var v3 = new NatsV3Format(metadataHeader);

        var wire = v3.Write(new Message { Topic = "t", Payload = "p", BinaryPayload = [1] });

        Assert.Equal("written", wire.Headers![NatsHeaderNames.Metadata].ToString());
        Assert.Equal("from header", v3.Read("t", new NatsBody(wire.BinaryBody), wire.Headers)!.Payload);
    }

    [Fact]
    public void Formats_DropAMessageWhoseMetadataTheHeaderCannotRead()
    {
        var metadataHeader = Substitute.For<INatsMetadataHeader>();
        metadataHeader.TryRead(Arg.Any<NatsHeaders?>(), out Arg.Any<MessageMetadataDtoV2?>()).Returns(false);

        Assert.Null(new NatsV2Format(metadataHeader).Read("t", Utf8("p"), null));
        Assert.Null(new NatsV3Format(metadataHeader).Read("t", Utf8("p"), null));
    }
}
