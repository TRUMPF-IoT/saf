// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.Tests.WireFormat;

using Interfaces;
using NSubstitute;
using SAF.Communication.PubSub.Cde.WireFormat;
using SAF.Messaging.Contracts;
using Xunit;

public class TsmMessageCodecTests
{
    private readonly TsmMessageCodec _codec = TsmWireFormats.CreateCodec();

    [Theory]
    [InlineData("$$batch:size=3$$", PubSubVersion.V4, true)]
    [InlineData("$$batch:size=3$$", PubSubVersion.V5, true)]
    [InlineData("$$batch:size=3$$", PubSubVersion.V3, false)]
    [InlineData("saf/topic", PubSubVersion.V4, false)]
    [InlineData("saf/$$batch", PubSubVersion.V4, false)]
    public void IsBatch_RequiresV4AndTheBatchChannel(string channel, string version, bool expected)
        => Assert.Equal(expected, _codec.IsBatch(channel, version));

    [Fact]
    public void BatchChannel_IsRecognizedAsBatch()
        => Assert.True(_codec.IsBatch(TsmBatchChannel.Create(7), PubSubVersion.V4));

    [Theory]
    [InlineData(PubSubVersion.V2)]
    [InlineData(PubSubVersion.V3)]
    [InlineData(PubSubVersion.V4)]
    [InlineData(PubSubVersion.V5)]
    public void EncodeAndDecode_RoundTripTheWholeMessage(string version)
    {
        var message = new Message
        {
            Topic = "t",
            Payload = "p",
            AcceptedReplyFormats = MessageFormats.Text | MessageFormats.Binary,
            CustomProperties = [new MessageCustomProperty { Name = "n", Value = "v" }]
        };

        var decoded = _codec.Decode("ignored", version, _codec.Encode(message, version));

        Assert.Equal("t", decoded!.Topic);
        Assert.Equal("p", decoded.Payload);
        Assert.Equal(MessageFormats.Text | MessageFormats.Binary, decoded.AcceptedReplyFormats);
        Assert.Equal("n", Assert.Single(decoded.CustomProperties!).Name);
        Assert.Equal("v", decoded.CustomProperties![0].Value);
        Assert.Null(decoded.BinaryPayload);
    }

    [Fact]
    public void EncodeAndDecode_V1KeepsThePayloadAndTakesTheTopicFromTheChannel()
    {
        var message = new Message
        {
            Topic = "t",
            Payload = "p",
            AcceptedReplyFormats = MessageFormats.Text,
            CustomProperties = [new MessageCustomProperty { Name = "n" }]
        };

        var decoded = _codec.Decode("channel", PubSubVersion.V1, _codec.Encode(message, PubSubVersion.V1));

        Assert.Equal("channel", decoded!.Topic);
        Assert.Equal("p", decoded.Payload);
        Assert.Null(decoded.AcceptedReplyFormats);
        Assert.Null(decoded.CustomProperties);
    }

    [Fact]
    public void EncodeAndDecode_V5RoundTripsTheBinaryPayload()
    {
        var message = new Message { Topic = "t", Payload = "p", BinaryPayload = [1, 2, 3] };

        var decoded = _codec.Decode("ignored", PubSubVersion.V5, _codec.Encode(message, PubSubVersion.V5));

        Assert.Equal("p", decoded!.Payload);
        Assert.Equal([1, 2, 3], decoded.BinaryPayload);
    }

    /// <summary>
    /// A newer node may accept reply formats this node does not know yet.
    /// </summary>
    [Fact]
    public void Decode_KeepsUnknownReplyFormatFlags()
        => Assert.Equal((MessageFormats)7,
            _codec.Decode("t", PubSubVersion.V4, new TsmPayload("""{"Topic":"t","AcceptedReplyFormats":7}"""))!.AcceptedReplyFormats);

    [Theory]
    [InlineData(PubSubVersion.V1)]
    [InlineData(PubSubVersion.V2)]
    [InlineData(PubSubVersion.V3)]
    [InlineData(PubSubVersion.V4)]
    public void CanEncode_RejectsBinaryPayloadsBeforeV5(string version)
    {
        Assert.True(_codec.CanEncode(new Message { Topic = "t", Payload = "p" }, version));
        Assert.False(_codec.CanEncode(new Message { Topic = "t", Payload = "p", BinaryPayload = [] }, version));
    }

    [Theory]
    [InlineData(PubSubVersion.V5)]
    [InlineData("5.1.0")]
    [InlineData("6.0.0")]
    public void CanEncode_AcceptsBinaryPayloadsFromV5(string version)
        => Assert.True(_codec.CanEncode(new Message { Topic = "t", BinaryPayload = [] }, version));

    [Fact]
    public void Encode_RefusesAMessageThePeerCannotReceive()
        => Assert.Throws<InvalidOperationException>(() => _codec.Encode(new Message { Topic = "t", BinaryPayload = [1] }, PubSubVersion.V3));

    [Fact]
    public void EncodeBatch_RefusesAMessageThePeerCannotReceive()
        => Assert.Throws<InvalidOperationException>(() =>
            _codec.EncodeBatch([new Message { Topic = "a" }, new Message { Topic = "b", BinaryPayload = [1] }], PubSubVersion.V4));

    [Theory]
    [InlineData(PubSubVersion.V4)]
    [InlineData(PubSubVersion.V5)]
    public void EncodeBatchAndDecodeBatch_RoundTripAllMessages(string version)
    {
        Message[] messages = [new() { Topic = "a", Payload = "1" }, new() { Topic = "b" }];

        var decoded = _codec.DecodeBatch(version, _codec.EncodeBatch(messages, version));

        Assert.Equal(["a", "b"], decoded!.Select(m => m.Topic));
        Assert.Equal(["1", null], decoded!.Select(m => m.Payload));
    }

    [Fact]
    public void EncodeBatch_IsNotSupportedForAFormatWithoutBatches()
        => Assert.Throws<NotSupportedException>(() => _codec.EncodeBatch([new Message { Topic = "t" }], PubSubVersion.V1));

    [Fact]
    public void Encode_IsNotSupportedBelowTheOldestFormat()
        => Assert.Throws<NotSupportedException>(() => _codec.Encode(new Message { Topic = "t" }, "0.9.0"));

    [Theory]
    [InlineData("t", PubSubVersion.V2, "null")]
    [InlineData("$$batch:size=1$$", PubSubVersion.V4, "null")]
    [InlineData("$$batch:size=1$$", PubSubVersion.V5, """[{"Topic":"t","BinaryPayloadLength":1}]""")]
    public void DecodeMessages_ReturnsNull_ForAnUnreadablePayload(string channel, string version, string pls)
        => Assert.Null(_codec.DecodeMessages(new Topic(channel, "id", version), new TsmPayload(pls)));

    /// <summary>
    /// A new pub/sub version is added by registering a format, without touching the codec or the older formats.
    /// </summary>
    [Fact]
    public void Codec_UsesTheNewestFormatThePeerVersionReaches()
    {
        var v6 = Substitute.For<ITsmMessageFormat>();
        v6.MinimumVersion.Returns(new Version(6, 0, 0));
        v6.CanEncode(Arg.Any<Message>()).Returns(true);
        v6.Encode(Arg.Any<Message>()).Returns(new TsmPayload("v6"));
        var codec = new TsmMessageCodec([..TsmWireFormats.All, v6]);
        var message = new Message { Topic = "t", Payload = "p" };

        Assert.Equal("v6", codec.Encode(message, "6.0.0").Pls);
        Assert.Equal("v6", codec.Encode(message, "6.3.0").Pls);
        Assert.Equal(_codec.Encode(message, PubSubVersion.V5), codec.Encode(message, PubSubVersion.V5));
        Assert.Equal(_codec.Encode(message, PubSubVersion.V4), codec.Encode(message, PubSubVersion.V4));
        Assert.Equal("p", codec.Encode(message, PubSubVersion.V1).Pls);
    }
}
