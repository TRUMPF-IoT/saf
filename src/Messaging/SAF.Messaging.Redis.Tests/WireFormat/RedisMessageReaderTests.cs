// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.Tests.WireFormat;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SAF.Messaging.Contracts;
using SAF.Messaging.Redis.WireFormat;
using TestUtilities;
using Xunit;

public class RedisMessageReaderTests
{
    [Fact]
    public void Read_ReadsMinorVersionOfV2AndIgnoresUnknownFields()
    {
        var message = TestWireFormat.Reader().Read("channel",
            """{"version":"2.1.0","message":{"topic":"t","payload":"p","futureField":1},"extra":1}""");

        Assert.Equal("t", message!.Topic);
        Assert.Equal("p", message.Payload);
    }

    /// <summary>
    /// A newer node may accept reply formats this node does not know yet.
    /// </summary>
    [Fact]
    public void Read_KeepsUnknownReplyFormatFlags()
    {
        var message = TestWireFormat.Reader().Read("channel",
            """{"version":"2.0.0","message":{"topic":"t","acceptedReplyFormats":7}}""");

        Assert.Equal((MessageFormats)7, message!.AcceptedReplyFormats);
    }

    [Fact]
    public void Read_ReadsEnvelopeWithoutVersion()
    {
        var message = TestWireFormat.Reader().Read("channel", """{"message":{"topic":"t","payload":"p"}}""");

        Assert.Equal("t", message!.Topic);
        Assert.Equal("p", message.Payload);
    }

    [Theory]
    [InlineData("""{"version":"3.0.0","message":{"topic":"t","payload":"p"}}""", "3.0.0")]
    [InlineData("""{"version":"99.1.0","message":{"topic":"t"}}""", "99.1.0")]
    [InlineData("""{"version":"0.9.0","message":{"topic":"t"}}""", "0.9.0")]
    [InlineData("""{"version":"abc","message":{"topic":"t"}}""", "abc")]
    [InlineData("""{"version":"3.0.0"}""", "3.0.0")]
    [InlineData("""{"version":"3.0.0","message":"opaque"}""", "3.0.0")]
    [InlineData("""{"version":"3.0.0","message":[1,2,3]}""", "3.0.0")]
    [InlineData("""{"version":"3.0.0","message":{"topic":42}}""", "3.0.0")]
    public void Read_DropsUnknownVersionsAndWarns(string wireValue, string expectedVersion)
    {
        var logger = Substitute.For<MockLogger>();

        var message = TestWireFormat.Reader(logger).Read("channel", wireValue);

        Assert.Null(message);
        logger.Received(1).Log(LogLevel.Warning, Arg.Is<string>(m => m.Contains(expectedVersion)));
    }

    [Theory]
    [InlineData("""{"version":"2.0.0","message":"opaque"}""")]
    [InlineData("""{"version":"2.0.0","message":{"topic":42}}""")]
    [InlineData("""{"version":"2.0.0"}""")]
    [InlineData("""{"version":"2.0.0","message":null}""")]
    [InlineData("""{"foo":1}""")]
    [InlineData("""[1,2,3]""")]
    [InlineData("""null""")]
    [InlineData("not json at all")]
    [InlineData("")]
    public void Read_FallsBackToRawValue_WhenNoKnownMessageCanBeRead(string wireValue)
    {
        var message = TestWireFormat.Reader().Read("channel", wireValue);

        Assert.Equal("channel", message!.Topic);
        Assert.Equal(wireValue, message.Payload);
    }

    /// <summary>
    /// A new envelope version is added by registering a format, without touching the reader.
    /// </summary>
    [Fact]
    public void Read_DispatchesToTheFormatRegisteredForTheMajorVersion()
    {
        var expected = new Message { Topic = "t" };
        var v3 = Substitute.For<IRedisEnvelopeFormat>();
        v3.MajorVersions.Returns([3]);
        v3.Read(Arg.Any<string>()).Returns(expected);
        var reader = new RedisMessageReader([..RedisWireFormats.All, v3], new UnknownVersionWarning(NullLogger.Instance));

        var message = reader.Read("channel", """{"version":"3.2.0","message":{}}""");

        Assert.Same(expected, message);
    }

    [Fact]
    public void Constructor_RejectsTwoFormatsForTheSameMajorVersion()
    {
        var duplicate = Substitute.For<IRedisEnvelopeFormat>();
        duplicate.MajorVersions.Returns([2]);

        Assert.Throws<ArgumentException>(() =>
            new RedisMessageReader([..RedisWireFormats.All, duplicate], new UnknownVersionWarning(NullLogger.Instance)));
    }
}
