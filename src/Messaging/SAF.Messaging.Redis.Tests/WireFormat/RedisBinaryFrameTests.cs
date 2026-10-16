// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.Tests.WireFormat;

using SAF.Messaging.Redis.WireFormat;
using StackExchange.Redis;
using Xunit;

public class RedisBinaryFrameTests
{
    private readonly RedisBinaryFrame _frame = new();

    [Theory]
    [InlineData("{}", new byte[] { 0, 255 })]
    [InlineData("""{"message":{"payload":"äöü"}}""", new byte[] { 1 })]
    [InlineData("", new byte[0])]
    public void Read_ReturnsWhatWriteWrote(string envelope, byte[] binaryPayload)
    {
        var value = _frame.Read(_frame.Write(envelope, binaryPayload));

        Assert.Equal(envelope, value!.Value.Envelope);
        Assert.Equal(binaryPayload, value.Value.BinaryPayload);
    }

    [Fact]
    public void Read_ReturnsAnEmptyBinaryPayload_WhenNothingFollowsTheEnvelope()
        => Assert.Empty(_frame.Read(_frame.Write("{}", []))!.Value.BinaryPayload!);

    [Fact]
    public void IsFrame_RecognizesTheMagic()
    {
        Assert.True(_frame.IsFrame(_frame.Write("{}", [])));
        Assert.True(_frame.IsFrame("SAFB"u8.ToArray()));
        Assert.False(_frame.IsFrame("""{"version":"2.0.0"}"""));
        Assert.False(_frame.IsFrame("SAF"u8.ToArray()));
        Assert.False(_frame.IsFrame(RedisValue.EmptyString));
        Assert.False(_frame.IsFrame(RedisValue.Null));
    }

    [Theory]
    [InlineData(new byte[] { 0x53, 0x41, 0x46, 0x42 }, null)]
    [InlineData(new byte[] { 0x53, 0x41, 0x46, 0x42, 0 }, null)]
    [InlineData(new byte[] { 0x53, 0x41, 0x46, 0x42, 1 }, null)]
    [InlineData(new byte[] { 0x53, 0x41, 0x46, 0x42, 7 }, (byte)7)]
    public void GetNewerLayoutVersion_ReportsOnlyLayoutsAfterThisOne(byte[] frame, byte? expected)
        => Assert.Equal(expected, _frame.GetNewerLayoutVersion(frame));

    [Fact]
    public void Read_ReturnsNull_ForAFrameShorterThanItsPrefix()
        => Assert.Null(_frame.Read(new byte[] { 0x53, 0x41, 0x46, 0x42, 1, 0, 0, 0 }));

    [Fact]
    public void Read_ReturnsNull_ForAnotherLayoutVersion()
    {
        var frame = (byte[])_frame.Write("{}", [1])!;
        frame[4] = 2;

        Assert.Null(_frame.Read(frame));
    }

    [Theory]
    [InlineData(3u)]
    [InlineData(uint.MaxValue)]
    public void Read_ReturnsNull_WhenTheEnvelopeLengthExceedsTheFrame(uint envelopeLength)
    {
        var frame = (byte[])_frame.Write("{}", [])!;
        BitConverter.TryWriteBytes(frame.AsSpan(5), envelopeLength);

        Assert.Null(_frame.Read(frame));
    }
}
