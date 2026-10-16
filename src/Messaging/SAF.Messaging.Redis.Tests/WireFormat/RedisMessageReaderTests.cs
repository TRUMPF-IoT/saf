// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.Tests.WireFormat;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SAF.Messaging.Contracts;
using SAF.Messaging.Redis.WireFormat;
using StackExchange.Redis;
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
    [InlineData("""{"version":"4.0.0","message":{"topic":"t","payload":"p"}}""", "4.0.0")]
    [InlineData("""{"version":"99.1.0","message":{"topic":"t"}}""", "99.1.0")]
    [InlineData("""{"version":"0.9.0","message":{"topic":"t"}}""", "0.9.0")]
    [InlineData("""{"version":"abc","message":{"topic":"t"}}""", "abc")]
    [InlineData("""{"version":"4.0.0"}""", "4.0.0")]
    [InlineData("""{"version":"4.0.0","message":"opaque"}""", "4.0.0")]
    [InlineData("""{"version":"4.0.0","message":[1,2,3]}""", "4.0.0")]
    [InlineData("""{"version":"4.0.0","message":{"topic":42}}""", "4.0.0")]
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
        var v4 = Substitute.For<IRedisEnvelopeFormat>();
        v4.MajorVersions.Returns([4]);
        v4.Read(Arg.Any<RedisWireValue>()).Returns(expected);
        var reader = new RedisMessageReader([..RedisWireFormats.All, v4], new RedisBinaryFrame(), new UnknownVersionWarning(NullLogger.Instance), NullLogger.Instance);

        var message = reader.Read("channel", """{"version":"4.2.0","message":{}}""");

        Assert.Same(expected, message);
    }

    [Fact]
    public void Constructor_RejectsTwoFormatsForTheSameMajorVersion()
    {
        var duplicate = Substitute.For<IRedisEnvelopeFormat>();
        duplicate.MajorVersions.Returns([2]);

        Assert.Throws<ArgumentException>(() =>
            new RedisMessageReader([..RedisWireFormats.All, duplicate], new RedisBinaryFrame(), new UnknownVersionWarning(NullLogger.Instance), NullLogger.Instance));
    }

    /// <summary>
    /// A version 3 envelope belongs into a binary frame. As plain text it is unreadable, like a broken envelope of
    /// any known version.
    /// </summary>
    [Fact]
    public void Read_FallsBackToRawValue_ForAVersion3EnvelopeOutsideAFrame()
    {
        const string value = """{"version":"3.0.0","message":{"topic":"t","payload":"p"}}""";

        var message = TestWireFormat.Reader().Read("channel", value);

        Assert.Equal(value, message!.Payload);
        Assert.Null(message.BinaryPayload);
    }

    [Fact]
    public void Read_DropsAFrameOfAnUnknownLayoutVersionAndWarns()
    {
        var logger = Substitute.For<MockLogger>();

        var message = TestWireFormat.Reader(logger).Read("channel", Frame("""{"version":"3.0.0","message":{"topic":"t"}}""", layoutVersion: 2));

        Assert.Null(message);
        logger.Received(1).Log(LogLevel.Warning, Arg.Is<string>(m => m.Contains("binary frame 2")));
    }

    [Fact]
    public void Read_DropsAFrameWithAnUnknownEnvelopeVersionAndWarns()
    {
        var logger = Substitute.For<MockLogger>();

        var message = TestWireFormat.Reader(logger).Read("channel", Frame("""{"version":"4.0.0","message":{"topic":"t"}}"""));

        Assert.Null(message);
        logger.Received(1).Log(LogLevel.Warning, Arg.Is<string>(m => m.Contains("4.0.0")));
    }

    public static TheoryData<byte[]> UnreadableFrames =>
    [
        "SAFB"u8.ToArray(),
        Frame("""{"version":"3.0.0","message":{"topic":"t"}}""", layoutVersion: 0),
        // "SAFB", layout 1, envelope length 100, "{}"
        new byte[] { 0x53, 0x41, 0x46, 0x42, 1, 100, 0, 0, 0, 0x7B, 0x7D },
        Frame("not json"),
        Frame("null"),
        Frame("""{"version":"3.0.0","message":"opaque"}"""),
        // A frame always carries a version 3 envelope.
        Frame("""{"version":"2.0.0","message":{"topic":"t"}}"""),
        Frame("""{"message":{"topic":"t"}}""")
    ];

    [Theory]
    [MemberData(nameof(UnreadableFrames))]
    public void Read_DropsAnUnreadableFrameAndWarns(byte[] frame)
    {
        var logger = Substitute.For<MockLogger>();

        var message = TestWireFormat.Reader(logger).Read("channel", frame);

        Assert.Null(message);
        logger.Received(1).Log(LogLevel.Warning, Arg.Is<string>(m => m.Contains("binary frame cannot be read")));
    }

    /// <summary>
    /// The reader leaves the frame layout to the injected binary frame.
    /// </summary>
    [Fact]
    public void Read_UnpacksFramesWithTheInjectedBinaryFrame()
    {
        RedisValue value = "anything";
        var binaryFrame = Substitute.For<IRedisBinaryFrame>();
        binaryFrame.IsFrame(value).Returns(true);
        binaryFrame.Read(value).Returns(new RedisWireValue("""{"version":"3.0.0","message":{"topic":"t"}}""", [7]));
        var reader = new RedisMessageReader(RedisWireFormats.All, binaryFrame, new UnknownVersionWarning(NullLogger.Instance), NullLogger.Instance);

        var message = reader.Read("channel", value);

        Assert.Equal(new byte[] { 7 }, message!.BinaryPayload);
    }

    private static byte[] Frame(string envelope, byte layoutVersion = RedisBinaryFrame.LayoutVersion)
    {
        var frame = (byte[])new RedisBinaryFrame().Write(envelope, [1, 2])!;
        frame[4] = layoutVersion;
        return frame;
    }
}
