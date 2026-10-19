// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.Tests.FileTransfer.Chunks;

using SAF.Messaging.Contracts;
using SAF.Toolbox.FileTransfer.Chunks;
using Xunit;
using static FileTransferGolden;

public class BinaryFileChunkReaderTests
{
    private readonly BinaryFileChunkReader _reader = new();

    [Fact]
    public void ReadsBinary_OnlyFromMessagesWithBinaryPayload()
    {
        Assert.Equal(MessageFormats.Binary, _reader.Format);
        Assert.True(_reader.CanRead(new Message { Topic = "t", Payload = BinaryChunkRequest, BinaryPayload = [] }));
        Assert.False(_reader.CanRead(new Message { Topic = "t", Payload = TextChunkRequest }));
    }

    [Fact]
    public void Read_TakesTheChunkDataFromTheBinaryPayload()
    {
        var data = ChunkData;

        var request = _reader.Read(new Message { Topic = "t", Payload = BinaryChunkRequest, BinaryPayload = data, ReplyTo = "reply/1" })!;

        AssertIsFile(request.File);
        Assert.Equal(1u, request.Chunk.Index);
        Assert.Same(data, request.Chunk.Data);
        Assert.Equal("reply/1", request.ReplyTo);
    }

    [Theory]
    [InlineData(null, "reply/1")]
    [InlineData(BinaryChunkRequest, null)]
    [InlineData(" ", "reply/1")]
    public void Read_ReturnsNull_WithoutHeaderOrReplyTopic(string? payload, string? replyTo)
    {
        Assert.Null(_reader.Read(new Message { Topic = "t", Payload = payload, BinaryPayload = ChunkData, ReplyTo = replyTo }));
    }
}
