// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.Tests.WireFormat;

using NSubstitute;
using SAF.Messaging.Contracts;
using SAF.Messaging.Redis.WireFormat;
using Xunit;

public class RedisEnvelopeFormatTests
{
    private const string V3Envelope = """{"version":"3.0.0","message":{"topic":"t","payload":"p"}}""";

    private readonly RedisEnvelopeV2Format _v2 = new(new RedisEnvelopeSerializer());
    private readonly RedisEnvelopeV3Format _v3 = new(new RedisEnvelopeSerializer());

    [Fact]
    public void V2_WritesTextOnly()
    {
        Assert.True(_v2.CanWrite(new Message { Topic = "t", Payload = "p" }));
        Assert.False(_v2.CanWrite(new Message { Topic = "t", BinaryPayload = [] }));
    }

    [Fact]
    public void V2_WritesAnEnvelopeWithoutBinaryPayload()
    {
        var value = _v2.Write(new Message { Topic = "t", Payload = "p" });

        Assert.Equal(new RedisWireValue("""{"version":"2.0.0","message":{"topic":"t","payload":"p"}}"""), value);
    }

    [Fact]
    public void V2_DoesNotReadABinaryPayload()
        => Assert.Null(_v2.Read(new RedisWireValue("""{"version":"2.0.0","message":{"topic":"t"}}""", [1])));

    [Fact]
    public void V3_WritesBinaryPayloadsOnly()
    {
        Assert.True(_v3.CanWrite(new Message { Topic = "t", BinaryPayload = [] }));
        Assert.False(_v3.CanWrite(new Message { Topic = "t", Payload = "p" }));
    }

    [Fact]
    public void V3_WritesTheVersion3EnvelopeAndTheBinaryPayloadWithoutCopying()
    {
        byte[] binaryPayload = [1, 2];

        var value = _v3.Write(new Message { Topic = "t", Payload = "p", BinaryPayload = binaryPayload });

        Assert.Equal(V3Envelope, value.Envelope);
        Assert.Same(binaryPayload, value.BinaryPayload);
    }

    [Fact]
    public void V3_TakesTheBinaryPayloadWithoutCopying()
    {
        byte[] binaryPayload = [1, 2, 3];

        var message = _v3.Read(new RedisWireValue(V3Envelope, binaryPayload));

        Assert.Equal("p", message!.Payload);
        Assert.Same(binaryPayload, message.BinaryPayload);
    }

    [Fact]
    public void V3_ReturnsNull_WithoutBinaryPayload()
        => Assert.Null(_v3.Read(new RedisWireValue(V3Envelope)));

    [Fact]
    public void V3_ReturnsNull_ForAnUnreadableMessage()
        => Assert.Null(_v3.Read(new RedisWireValue("""{"version":"3.0.0","message":"opaque"}""", [1])));

    [Fact]
    public void V3_RoundTripsAMessage()
    {
        var message = new Message
        {
            Topic = "t",
            Payload = "p",
            BinaryPayload = [0, 255],
            AcceptedReplyFormats = MessageFormats.Binary,
            CustomProperties = [new MessageCustomProperty { Name = "n", Value = "v" }]
        };

        var read = _v3.Read(_v3.Write(message))!;

        Assert.Equal("t", read.Topic);
        Assert.Equal("p", read.Payload);
        Assert.Equal(message.BinaryPayload, read.BinaryPayload);
        Assert.Equal(MessageFormats.Binary, read.AcceptedReplyFormats);
        Assert.Equal("v", Assert.Single(read.CustomProperties!).Value);
    }

    /// <summary>
    /// The formats leave the JSON to the injected serializer.
    /// </summary>
    [Fact]
    public void Formats_UseTheInjectedSerializer()
    {
        var message = new Message { Topic = "t", BinaryPayload = [1] };
        var serializer = Substitute.For<IRedisEnvelopeSerializer>();
        serializer.Serialize(RedisMessageVersion.V3, message).Returns("envelope");
        serializer.Deserialize("envelope").Returns(new Message { Topic = "read" });
        var v3 = new RedisEnvelopeV3Format(serializer);

        var value = v3.Write(message);

        Assert.Equal("envelope", value.Envelope);
        Assert.Equal("read", v3.Read(value)!.Topic);
    }
}
