// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.Tests.WireFormat;

using NSubstitute;
using SAF.Messaging.Contracts;
using SAF.Messaging.Redis.WireFormat;
using StackExchange.Redis;
using Xunit;

public class BinaryPayloadsDisabledWriterTests
{
    private readonly IRedisMessageWriter _inner = Substitute.For<IRedisMessageWriter>();
    private readonly BinaryPayloadsDisabledWriter _writer;

    public BinaryPayloadsDisabledWriterTests()
    {
        _writer = new BinaryPayloadsDisabledWriter(_inner);
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 1 })]
    public void DropsBinaryPayloadsWithAReasonThatNamesTheSetting(byte[] binaryPayload)
    {
        Assert.False(_writer.TryWrite(new Message { Topic = "t", BinaryPayload = binaryPayload }, out var value, out var dropReason));

        Assert.True(value.IsNull);
        Assert.Contains("EnableBinaryPayloads", dropReason);
        _inner.DidNotReceiveWithAnyArgs().TryWrite(default!, out _, out _);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "inner reason")]
    public void PassesTextMessagesToTheWriter(bool written, string? reason)
    {
        var message = new Message { Topic = "t", Payload = "p" };
        _inner.TryWrite(message, out Arg.Any<RedisValue>(), out Arg.Any<string?>())
            .Returns(ci =>
            {
                ci[1] = (RedisValue)"value";
                ci[2] = reason;
                return written;
            });

        Assert.Equal(written, _writer.TryWrite(message, out var value, out var dropReason));
        Assert.Equal("value", value);
        Assert.Equal(reason, dropReason);
    }
}
