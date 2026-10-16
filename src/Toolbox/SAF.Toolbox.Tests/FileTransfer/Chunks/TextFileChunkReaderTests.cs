// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.Tests.FileTransfer.Chunks;

using SAF.Messaging.Contracts;
using SAF.Toolbox.FileTransfer.Chunks;
using Xunit;
using static FileTransferGolden;

public class TextFileChunkReaderTests
{
    private readonly TextFileChunkReader _reader = new();

    [Fact]
    public void ReadsText_OnlyFromMessagesWithoutBinaryPayload()
    {
        Assert.Equal(MessageFormats.Text, _reader.Format);
        Assert.True(_reader.CanRead(new Message { Topic = "t", Payload = TextChunkRequest }));
        Assert.False(_reader.CanRead(new Message { Topic = "t", Payload = BinaryChunkRequest, BinaryPayload = ChunkData }));
    }

    [Fact]
    public void Read_ReadsTheRequestOfASaf10Sender()
    {
        var request = _reader.Read(new Message { Topic = "t", Payload = TextChunkRequest })!;

        AssertIsFile(request.File);
        Assert.Equal(1u, request.Chunk.Index);
        Assert.Equal(ChunkData, request.Chunk.Data);
        Assert.Equal("reply/1", request.ReplyTo);
    }

    [Fact]
    public void Read_PrefersTheReplyTopicOfTheMessage()
    {
        Assert.Equal("reply/2", _reader.Read(new Message { Topic = "t", Payload = TextChunkRequest, ReplyTo = "reply/2" })!.ReplyTo);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("""{"file":{"fileName":"f","fileId":"i","contentHash":"h","contentLength":1,"chunkSize":1,"totalChunks":1},"replyTo":"r"}""", null)]
    [InlineData("""{"file":{"fileName":"f","fileId":"i","contentHash":"h","contentLength":1,"chunkSize":1,"totalChunks":1},"fileChunk":{"index":0,"data":"AQ=="}}""", null)]
    [InlineData(" ", "reply/2")]
    public void Read_ReturnsNull_WithoutChunkOrReplyTopic(string? payload, string? replyTo)
    {
        Assert.Null(_reader.Read(new Message { Topic = "t", Payload = payload, ReplyTo = replyTo }));
    }
}
