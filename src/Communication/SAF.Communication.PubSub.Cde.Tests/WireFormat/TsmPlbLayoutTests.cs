// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.Tests.WireFormat;

using NSubstitute;
using SAF.Communication.PubSub.Cde.WireFormat;
using SAF.Messaging.Contracts;
using Xunit;

public class TsmPlbLayoutTests
{
    private readonly TsmPlbLayout _layout = new();

    [Fact]
    public void Pack_ReturnsNull_WithoutBytes()
    {
        Assert.Null(_layout.Pack([]));
        Assert.Null(_layout.Pack([[], []]));
    }

    [Fact]
    public void Pack_UsesASingleOddBinaryPayloadWithoutCopying()
    {
        byte[] binaryPayload = [1, 2, 3];

        Assert.Same(binaryPayload, _layout.Pack([[], binaryPayload]));
    }

    [Fact]
    public void Pack_JoinsTheBinaryPayloadsInOrder()
        => Assert.Equal(new byte[] { 1, 2, 3 }, _layout.Pack([[1], [], [2, 3]]));

    [Fact]
    public void Pack_PadsAnEvenTotalLength()
        => Assert.Equal(new byte[] { 1, 2, 0 }, _layout.Pack([[1], [2]]));

    [Fact]
    public void Pack_PadsASingleEvenBinaryPayload()
        => Assert.Equal(new byte[] { 1, 2, 0 }, _layout.Pack([[1, 2]]));

    [Fact]
    public void Unpack_ReturnsAPlbThatOneBinaryPayloadFillsWithoutCopying()
    {
        byte[] plb = [1, 2, 3];

        Assert.Same(plb, Assert.Single(_layout.Unpack(plb, [3])!));
    }

    [Fact]
    public void Unpack_CutsPlbInOrderAndIgnoresThePadding()
    {
        var binaryPayloads = _layout.Unpack([1, 2, 0], [1, 0, 1])!;

        Assert.Equal([[1], [], [2]], binaryPayloads);
    }

    [Fact]
    public void Unpack_ReadsEmptyBinaryPayloadsWithoutPlb()
        => Assert.Equal([[], []], _layout.Unpack(null, [0, 0])!);

    [Theory]
    [InlineData(new byte[] { 1, 2, 3 }, new[] { 4 })]
    [InlineData(new byte[] { 1, 2, 3 }, new[] { 1 })]
    [InlineData(new byte[] { 1, 2 }, new[] { 2 })]
    [InlineData(new byte[] { 1, 2, 3 }, new[] { -1 })]
    [InlineData(null, new[] { 1 })]
    [InlineData(new byte[] { 1 }, new int[0])]
    public void Unpack_ReturnsNull_WhenTheLengthsDoNotMatch(byte[]? plb, int[] lengths)
        => Assert.Null(_layout.Unpack(plb, lengths));

    /// <summary>
    /// The V5 format leaves <c>PLB</c> to the injected layout.
    /// </summary>
    [Fact]
    public void V5_UsesTheInjectedLayout()
    {
        byte[] binaryPayload = [9];
        var layout = Substitute.For<ITsmPlbLayout>();
        layout.Pack(Arg.Is<IReadOnlyList<byte[]>>(p => p.Single() == binaryPayload)).Returns([7, 7]);
        layout.Unpack(Arg.Is<byte[]?>(p => p!.Length == 2), Arg.Is<IReadOnlyList<int>>(l => l.Single() == 1)).Returns([[5]]);
        var v5 = new TsmMessageFormatV5(layout);

        var payload = v5.Encode(new Message { Topic = "t", BinaryPayload = binaryPayload });
        var message = v5.Decode("t", payload)!;

        Assert.Equal(new byte[] { 7, 7 }, payload.Plb);
        Assert.Equal(new byte[] { 5 }, message.BinaryPayload);
    }

    [Fact]
    public void V5_DropsAMessageWhoseLengthsTheLayoutRejects()
    {
        var layout = Substitute.For<ITsmPlbLayout>();
        layout.Unpack(Arg.Any<byte[]?>(), Arg.Any<IReadOnlyList<int>>()).Returns((IReadOnlyList<byte[]>?)null);

        Assert.Null(new TsmMessageFormatV5(layout).Decode("t", new TsmPayload("""{"Topic":"t","BinaryPayloadLength":1}""", [1])));
    }
}
