// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.Tests.WireFormat;

using NSubstitute;
using SAF.Messaging.Contracts;
using SAF.Messaging.Redis.WireFormat;
using StackExchange.Redis;
using Xunit;

public class RedisMessageWriterTests
{
    private readonly IRedisBinaryFrame _binaryFrame = Substitute.For<IRedisBinaryFrame>();

    [Fact]
    public void Write_UsesTheOldestFormatThatCanWriteTheMessage()
    {
        var message = new Message { Topic = "t" };
        var newer = CreateFormat(3, canWrite: true, "v3");
        var older = CreateFormat(2, canWrite: true, "v2");

        Assert.True(new RedisMessageWriter([newer, older], _binaryFrame).TryWrite(message, out var value, out var dropReason));
        Assert.Equal("v2", value);
        Assert.Null(dropReason);
    }

    [Fact]
    public void Write_SkipsFormatsThatCannotWriteTheMessage()
    {
        var message = new Message { Topic = "t" };
        var older = CreateFormat(2, canWrite: false, "v2");
        var newer = CreateFormat(3, canWrite: true, "v3");

        Assert.True(new RedisMessageWriter([older, newer], _binaryFrame).TryWrite(message, out var value, out _));
        Assert.Equal("v3", value);
    }

    [Fact]
    public void Write_FailsWithAReason_WhenNoFormatCanWriteTheMessage()
    {
        var writer = new RedisMessageWriter([CreateFormat(2, canWrite: false, "v2")], _binaryFrame);

        Assert.False(writer.TryWrite(new Message { Topic = "t", BinaryPayload = [] }, out var value, out var dropReason));
        Assert.True(value.IsNull);
        Assert.Contains("Binary", dropReason);
    }

    /// <summary>
    /// The writer leaves the frame layout to the injected binary frame.
    /// </summary>
    [Fact]
    public void Write_PacksAValueWithBinaryPayloadIntoTheBinaryFrame()
    {
        byte[] binaryPayload = [1];
        var format = CreateFormat(3, canWrite: true, new RedisWireValue("envelope", binaryPayload));
        _binaryFrame.Write("envelope", binaryPayload).Returns((RedisValue)"frame");

        Assert.True(new RedisMessageWriter([format], _binaryFrame).TryWrite(new Message { Topic = "t" }, out var value, out _));
        Assert.Equal("frame", value);
    }

    [Fact]
    public void Write_SendsAValueWithoutBinaryPayloadAsPlainText()
    {
        Assert.True(new RedisMessageWriter([CreateFormat(2, canWrite: true, "envelope")], _binaryFrame)
            .TryWrite(new Message { Topic = "t" }, out var value, out _));

        Assert.Equal("envelope", value);
        _binaryFrame.DidNotReceive().Write(Arg.Any<string>(), Arg.Any<byte[]>());
    }

    [Fact]
    public void Write_WritesABinaryFrameForBinaryPayloads()
    {
        Assert.True(TestWireFormat.Writer().TryWrite(new Message { Topic = "t", Payload = "p", BinaryPayload = [1] }, out var value, out _));
        Assert.True(new RedisBinaryFrame().IsFrame(value));
    }

    private static IRedisEnvelopeFormat CreateFormat(int major, bool canWrite, string envelope)
        => CreateFormat(major, canWrite, new RedisWireValue(envelope));

    private static IRedisEnvelopeFormat CreateFormat(int major, bool canWrite, RedisWireValue output)
    {
        var format = Substitute.For<IRedisEnvelopeFormat>();
        format.MajorVersions.Returns([major]);
        format.CanWrite(Arg.Any<Message>()).Returns(canWrite);
        format.Write(Arg.Any<Message>()).Returns(output);
        return format;
    }
}
