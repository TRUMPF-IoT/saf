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
    public void EncodeAndDecode_RoundTripTheWholeMessage(string version)
    {
        var message = new Message
        {
            Topic = "t",
            Payload = "p",
            CustomProperties = [new MessageCustomProperty { Name = "n", Value = "v" }]
        };

        var decoded = _codec.Decode("ignored", version, _codec.Encode(message, version));

        Assert.Equal("t", decoded!.Topic);
        Assert.Equal("p", decoded.Payload);
        Assert.Equal("n", Assert.Single(decoded.CustomProperties!).Name);
        Assert.Equal("v", decoded.CustomProperties![0].Value);
    }

    [Fact]
    public void EncodeAndDecode_V1KeepsThePayloadAndTakesTheTopicFromTheChannel()
    {
        var message = new Message { Topic = "t", Payload = "p", CustomProperties = [new MessageCustomProperty { Name = "n" }] };

        var decoded = _codec.Decode("channel", PubSubVersion.V1, _codec.Encode(message, PubSubVersion.V1));

        Assert.Equal("channel", decoded!.Topic);
        Assert.Equal("p", decoded.Payload);
        Assert.Null(decoded.CustomProperties);
    }

    [Fact]
    public void EncodeBatchAndDecodeBatch_RoundTripAllMessages()
    {
        Message[] messages = [new() { Topic = "a", Payload = "1" }, new() { Topic = "b" }];

        var decoded = _codec.DecodeBatch(PubSubVersion.V4, _codec.EncodeBatch(messages, PubSubVersion.V4));

        Assert.Equal(["a", "b"], decoded!.Select(m => m.Topic));
        Assert.Equal(["1", null], decoded!.Select(m => m.Payload));
    }

    [Fact]
    public void EncodeBatch_IsNotSupportedForAFormatWithoutBatches()
        => Assert.Throws<NotSupportedException>(() => _codec.EncodeBatch([new Message { Topic = "t" }], PubSubVersion.V1));

    [Fact]
    public void Encode_IsNotSupportedBelowTheOldestFormat()
        => Assert.Throws<NotSupportedException>(() => _codec.Encode(new Message { Topic = "t" }, "0.9.0"));

    /// <summary>
    /// A new pub/sub version is added by registering a format, without touching the codec or the older formats.
    /// </summary>
    [Fact]
    public void Codec_UsesTheNewestFormatThePeerVersionReaches()
    {
        var v5 = Substitute.For<ITsmMessageFormat>();
        v5.MinimumVersion.Returns(new Version(5, 0, 0));
        v5.Encode(Arg.Any<Message>()).Returns("v5");
        var codec = new TsmMessageCodec([..TsmWireFormats.All, v5]);
        var message = new Message { Topic = "t", Payload = "p" };

        Assert.Equal("v5", codec.Encode(message, "5.0.0"));
        Assert.Equal("v5", codec.Encode(message, "5.3.0"));
        Assert.Equal(_codec.Encode(message, PubSubVersion.V4), codec.Encode(message, PubSubVersion.V4));
        Assert.Equal("p", codec.Encode(message, PubSubVersion.V1));
    }
}
