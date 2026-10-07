// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.Tests.WireFormat;

using NSubstitute;
using SAF.Messaging.Contracts;
using SAF.Messaging.Nats.WireFormat;
using Xunit;

public class NatsMessageWriterTests
{
    [Fact]
    public void Write_UsesV1_WithoutCustomProperties()
        => Assert.Null(TestWireFormat.Writer().Write(new Message { Topic = "t", Payload = "p" }).Headers);

    [Fact]
    public void Write_UsesV2_WithCustomProperties()
    {
        var wire = TestWireFormat.Writer().Write(new Message { Topic = "t", Payload = "p", CustomProperties = [] });

        Assert.Equal("2.0.0", wire.Headers![NatsHeaderNames.Version].ToString());
    }

    [Fact]
    public void Write_UsesTheOldestFormatThatCanWriteTheMessage()
    {
        var newer = CreateFormat(3, canWrite: true, "v3");
        var older = CreateFormat(2, canWrite: true, "v2");
        var skipped = CreateFormat(1, canWrite: false, "v1");

        Assert.Equal("v2", new NatsMessageWriter([newer, skipped, older]).Write(new Message { Topic = "t" }).Body);
    }

    [Fact]
    public void Write_Throws_WhenNoFormatCanWriteTheMessage()
    {
        var writer = new NatsMessageWriter([CreateFormat(1, canWrite: false, "v1")]);

        Assert.Throws<InvalidOperationException>(() => writer.Write(new Message { Topic = "t" }));
    }

    private static INatsWireFormat CreateFormat(int major, bool canWrite, string body)
    {
        var format = Substitute.For<INatsWireFormat>();
        format.MajorVersion.Returns(major);
        format.CanWrite(Arg.Any<Message>()).Returns(canWrite);
        format.Write(Arg.Any<Message>()).Returns(new NatsWireMessage(body, null));
        return format;
    }
}
