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
    public void Write_UsesV1_WithoutMetadata()
    {
        Assert.True(TestWireFormat.Writer().TryWrite(new Message { Topic = "t", Payload = "p" }, out var wire, out _));
        Assert.Null(wire.Headers);
    }

    public static TheoryData<Message> MessagesWithMetadata =>
    [
        new Message { Topic = "t", Payload = "p", CustomProperties = [] },
        new Message { Topic = "t", Payload = "p", AcceptedReplyFormats = MessageFormats.Text }
    ];

    [Theory]
    [MemberData(nameof(MessagesWithMetadata))]
    public void Write_UsesV2_WithMetadata(Message message)
    {
        Assert.True(TestWireFormat.Writer().TryWrite(message, out var wire, out _));
        Assert.Equal("2.0.0", wire.Headers![NatsHeaderNames.Version].ToString());
    }

    [Fact]
    public void Write_UsesV3_ForBinaryPayloads()
    {
        Assert.True(TestWireFormat.Writer().TryWrite(new Message { Topic = "t", BinaryPayload = [1] }, out var wire, out _));
        Assert.Equal("3.0.0", wire.Headers![NatsHeaderNames.Version].ToString());
    }

    [Fact]
    public void Write_UsesTheOldestFormatThatCanWriteTheMessage()
    {
        var newer = CreateFormat(3, canWrite: true, "v3");
        var older = CreateFormat(2, canWrite: true, "v2");
        var skipped = CreateFormat(1, canWrite: false, "v1");

        Assert.True(new NatsMessageWriter([newer, skipped, older]).TryWrite(new Message { Topic = "t" }, out var wire, out var dropReason));
        Assert.Equal("v2", wire.TextBody);
        Assert.Null(dropReason);
    }

    [Fact]
    public void Write_FailsWithAReason_WhenNoFormatCanWriteTheMessage()
    {
        var writer = new NatsMessageWriter([CreateFormat(1, canWrite: false, "v1")]);

        Assert.False(writer.TryWrite(new Message { Topic = "t", BinaryPayload = [] }, out var wire, out var dropReason));
        Assert.Equal(default, wire);
        Assert.Contains("Binary", dropReason);
    }

    private static INatsWireFormat CreateFormat(int major, bool canWrite, string body)
    {
        var format = Substitute.For<INatsWireFormat>();
        format.MajorVersion.Returns(major);
        format.CanWrite(Arg.Any<Message>()).Returns(canWrite);
        format.Write(Arg.Any<Message>()).Returns(NatsWireMessage.Text(body, null));
        return format;
    }
}
