// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.Tests.FileTransfer.Chunks;

using NSubstitute;
using SAF.Messaging.Contracts;
using SAF.Toolbox.FileTransfer;
using SAF.Toolbox.FileTransfer.Chunks;
using SAF.Toolbox.RequestClient;
using Xunit;
using static FileTransferGolden;
using RequestOptions = SAF.Toolbox.RequestClient.RequestOptions;

public class BinaryFileChunkSenderTests
{
    private readonly IRequestClient _requestClient = Substitute.For<IRequestClient>();

    [Fact]
    public void SendsBinary()
        => Assert.Equal(MessageFormats.Binary, new BinaryFileChunkSender(_requestClient).Format);

    [Theory]
    [InlineData("""{"status":0}""", FileReceiverStatus.Ok)]
    [InlineData("""{"status":1}""", FileReceiverStatus.Failed)]
    public async Task SendAsync_SendsTheDataAsBinaryPayload_AndReturnsTheReceiverStatus(string reply, FileReceiverStatus expected)
    {
        var chunk = new FileChunk { Index = 1, Data = ChunkData };
        string? payload = null;
        byte[]? binaryPayload = null;
        RequestOptions? options = null;
        _requestClient.SendRequestAwaitFirstReply("topic", Arg.Do<string?>(p => payload = p), Arg.Do<byte[]?>(b => binaryPayload = b),
                Arg.Do<RequestOptions?>(o => options = o))
            .Returns(new Message { Topic = "reply", Payload = reply });

        var status = await new BinaryFileChunkSender(_requestClient).SendAsync("topic", File, chunk, 1000);

        Assert.Equal(expected, status);
        Assert.Equal(BinaryChunkRequest, payload);
        Assert.Same(chunk.Data, binaryPayload);
        Assert.Equal("private/data/transfer/receipt", options!.ReplyTopicPrefix);
        Assert.Equal(1000, options.MillisecondsTimeoutTarget);
        Assert.Null(options.AcceptedReplyFormats);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendAsync_ReturnsNull_WithoutAReplyPayload(bool replyArrives)
    {
        _requestClient.SendRequestAwaitFirstReply(default!).ReturnsForAnyArgs(replyArrives ? new Message { Topic = "reply", BinaryPayload = [1] } : null);

        Assert.Null(await new BinaryFileChunkSender(_requestClient).SendAsync("topic", File, new FileChunk { Index = 1, Data = ChunkData }, 1000));
    }
}
