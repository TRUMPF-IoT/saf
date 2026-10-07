// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.Tests.WireFormat;

using NSubstitute;
using SAF.Messaging.Contracts;
using SAF.Messaging.Redis.WireFormat;
using Xunit;

public class RedisMessageWriterTests
{
    [Fact]
    public void Write_UsesTheOldestFormatThatCanWriteTheMessage()
    {
        var message = new Message { Topic = "t" };
        var newer = CreateFormat(3, canWrite: true, "v3");
        var older = CreateFormat(2, canWrite: true, "v2");

        Assert.Equal("v2", new RedisMessageWriter([newer, older]).Write(message));
    }

    [Fact]
    public void Write_SkipsFormatsThatCannotWriteTheMessage()
    {
        var message = new Message { Topic = "t" };
        var older = CreateFormat(2, canWrite: false, "v2");
        var newer = CreateFormat(3, canWrite: true, "v3");

        Assert.Equal("v3", new RedisMessageWriter([older, newer]).Write(message));
    }

    [Fact]
    public void Write_Throws_WhenNoFormatCanWriteTheMessage()
    {
        var writer = new RedisMessageWriter([CreateFormat(2, canWrite: false, "v2")]);

        Assert.Throws<InvalidOperationException>(() => writer.Write(new Message { Topic = "t" }));
    }

    private static IRedisEnvelopeFormat CreateFormat(int major, bool canWrite, string output)
    {
        var format = Substitute.For<IRedisEnvelopeFormat>();
        format.MajorVersions.Returns([major]);
        format.CanWrite(Arg.Any<Message>()).Returns(canWrite);
        format.Write(Arg.Any<Message>()).Returns(output);
        return format;
    }
}
