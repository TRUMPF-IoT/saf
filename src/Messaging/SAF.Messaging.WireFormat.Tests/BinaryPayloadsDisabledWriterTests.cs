// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.WireFormat.Tests;

using NSubstitute;
using SAF.Messaging.Contracts;
using Xunit;

public class BinaryPayloadsDisabledWriterTests
{
    private readonly IWireMessageWriter<string?> _inner = Substitute.For<IWireMessageWriter<string?>>();
    private readonly BinaryPayloadsDisabledWriter<string?> _writer;

    public BinaryPayloadsDisabledWriterTests()
    {
        _writer = new BinaryPayloadsDisabledWriter<string?>(_inner);
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 1 })]
    public void DropsBinaryPayloadsWithAReasonThatNamesTheSetting(byte[] binaryPayload)
    {
        Assert.False(_writer.TryWrite(new Message { Topic = "t", BinaryPayload = binaryPayload }, out var wire, out var dropReason));

        Assert.Null(wire);
        Assert.Contains("EnableBinaryPayloads", dropReason);
        _inner.DidNotReceiveWithAnyArgs().TryWrite(default!, out _, out _);
    }

    [Theory]
    [InlineData(MessageFormats.Text | MessageFormats.Binary)]
    [InlineData(MessageFormats.Text)]
    public void Formats_AreTheWritersFormatsWithoutBinary(MessageFormats innerFormats)
    {
        _inner.Formats.Returns(innerFormats);

        Assert.Equal(MessageFormats.Text, _writer.Formats);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "inner reason")]
    public void PassesTextMessagesToTheWriter(bool written, string? reason)
    {
        var message = new Message { Topic = "t", Payload = "p" };
        _inner.TryWrite(message, out Arg.Any<string?>(), out Arg.Any<string?>())
            .Returns(ci =>
            {
                ci[1] = "wire";
                ci[2] = reason;
                return written;
            });

        Assert.Equal(written, _writer.TryWrite(message, out var wire, out var dropReason));
        Assert.Equal("wire", wire);
        Assert.Equal(reason, dropReason);
    }
}
