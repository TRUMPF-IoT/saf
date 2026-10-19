// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.Tests.WireFormat;

using NSubstitute;
using SAF.Communication.PubSub.Cde.WireFormat;
using SAF.Messaging.Contracts;
using Xunit;

public class TsmTextOnlyFormatTests
{
    private readonly ITsmBatchFormat _inner = Substitute.For<ITsmBatchFormat>();
    private readonly TsmTextOnlyFormat _format;

    public TsmTextOnlyFormatTests()
    {
        _inner.CanEncode(Arg.Any<Message>()).Returns(true);
        _format = new TsmTextOnlyFormat(_inner, new Version(2, 0, 0));
    }

    [Fact]
    public void MinimumVersion_IsTheGivenOne()
        => Assert.Equal(new Version(2, 0, 0), _format.MinimumVersion);

    [Fact]
    public void CanEncode_RejectsBinaryPayloads()
    {
        Assert.True(_format.CanEncode(new Message { Topic = "t", Payload = "p" }));
        Assert.False(_format.CanEncode(new Message { Topic = "t", BinaryPayload = [] }));
    }

    [Fact]
    public void CanEncode_AsksTheInnerFormat()
    {
        var message = new Message { Topic = "t" };
        _inner.CanEncode(message).Returns(false);

        Assert.False(_format.CanEncode(message));
    }

    [Fact]
    public void EncodesAndDecodesWithTheInnerFormat()
    {
        var message = new Message { Topic = "t" };
        var payload = new TsmPayload("pls");
        List<Message> messages = [message];
        _inner.Encode(message).Returns(payload);
        _inner.EncodeBatch(messages).Returns(payload);
        _inner.Decode("c", payload).Returns(message);
        _inner.DecodeBatch(payload).Returns(messages);

        Assert.Equal(payload, _format.Encode(message));
        Assert.Equal(payload, _format.EncodeBatch(messages));
        Assert.Same(message, _format.Decode("c", payload));
        Assert.Same(messages, _format.DecodeBatch(payload));
    }

    [Fact]
    public void Decode_PassesNoPlbToTheInnerFormat()
    {
        var message = new Message { Topic = "t" };
        List<Message> messages = [message];
        _inner.Decode("c", new TsmPayload("pls")).Returns(message);
        _inner.DecodeBatch(new TsmPayload("pls")).Returns(messages);

        Assert.Same(message, _format.Decode("c", new TsmPayload("pls", [1, 2])));
        Assert.Same(messages, _format.DecodeBatch(new TsmPayload("pls", [1, 2])));
    }
}
