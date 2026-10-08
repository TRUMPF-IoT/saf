// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.Tests.WireFormat;

using NSubstitute;
using SAF.Messaging.Contracts;
using SAF.Messaging.Nats.WireFormat;
using Xunit;

public class BinaryPayloadsDisabledWriterTests
{
    private readonly INatsMessageWriter _inner = Substitute.For<INatsMessageWriter>();
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
        Assert.False(_writer.TryWrite(new Message { Topic = "t", BinaryPayload = binaryPayload }, out var wire, out var dropReason));

        Assert.Equal(default, wire);
        Assert.Contains("EnableBinaryPayloads", dropReason);
        _inner.DidNotReceiveWithAnyArgs().TryWrite(default!, out _, out _);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "inner reason")]
    public void PassesTextMessagesToTheWriter(bool written, string? reason)
    {
        var message = new Message { Topic = "t", Payload = "p" };
        var innerWire = NatsWireMessage.Text("p", null);
        _inner.TryWrite(message, out Arg.Any<NatsWireMessage>(), out Arg.Any<string?>())
            .Returns(ci =>
            {
                ci[1] = innerWire;
                ci[2] = reason;
                return written;
            });

        Assert.Equal(written, _writer.TryWrite(message, out var wire, out var dropReason));
        Assert.Equal(innerWire, wire);
        Assert.Equal(reason, dropReason);
    }
}
