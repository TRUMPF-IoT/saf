// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.Tests.WireFormat;

using NATS.Client.Core;
using SAF.Messaging.Contracts;
using SAF.Messaging.Nats.WireFormat;
using Xunit;

public class NatsWireFormatTests
{
    private readonly NatsV1Format _v1 = new();
    private readonly NatsV2Format _v2 = new();

    [Fact]
    public void V1_WritesOnlyMessagesWithoutCustomProperties()
    {
        Assert.True(_v1.CanWrite(new Message { Topic = "t" }));
        Assert.False(_v1.CanWrite(new Message { Topic = "t", CustomProperties = [] }));
    }

    [Fact]
    public void V1_WritesThePayloadWithoutHeaders()
        => Assert.Equal(new NatsWireMessage("p", null), _v1.Write(new Message { Topic = "t", Payload = "p" }));

    [Fact]
    public void V1_ReadsThePayloadAndIgnoresHeaders()
    {
        var message = _v1.Read("t", "p", new NatsHeaders { { NatsHeaderNames.Metadata, "{}" } });

        Assert.Equal("t", message.Topic);
        Assert.Equal("p", message.Payload);
        Assert.Null(message.CustomProperties);
    }

    [Fact]
    public void V2_KeepsAnEmptyPropertyList()
    {
        var wire = _v2.Write(new Message { Topic = "t", CustomProperties = [] });

        Assert.Equal("""{"customProperties":[]}""", wire.Headers![NatsHeaderNames.Metadata].ToString());
        Assert.Empty(_v2.Read("t", null, wire.Headers)!.CustomProperties!);
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
        var read = _v2.Read("t", wire.Body, wire.Headers)!;

        Assert.All(wire.Headers![NatsHeaderNames.Metadata].ToString(), c => Assert.InRange(c, ' ', '~'));
        Assert.Equal("ü", read.CustomProperties![0].Name);
        Assert.Equal(value, read.CustomProperties[0].Value);
    }

    [Fact]
    public void V2_ReadsAMessageWithoutMetadata()
    {
        var message = _v2.Read("t", "p", new NatsHeaders { { NatsHeaderNames.Version, "2.0.0" } })!;

        Assert.Equal("p", message.Payload);
        Assert.Null(message.CustomProperties);
    }

    [Fact]
    public void V2_IgnoresUnknownMetadataFields()
    {
        var headers = new NatsHeaders { { NatsHeaderNames.Metadata, """{"customProperties":[{"name":"n","value":"v"}],"futureField":1}""" } };

        Assert.Equal("v", Assert.Single(_v2.Read("t", "p", headers)!.CustomProperties!).Value);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{"customProperties":"x"}""")]
    public void V2_ReturnsNull_ForUnreadableMetadata(string metadata)
        => Assert.Null(_v2.Read("t", "p", new NatsHeaders { { NatsHeaderNames.Metadata, metadata } }));
}
