// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.Tests.FileTransfer.Chunks;

using NSubstitute;
using SAF.Messaging.Contracts;
using SAF.Toolbox.FileTransfer;
using SAF.Toolbox.FileTransfer.Chunks;
using SAF.Toolbox.FileTransfer.Messages;
using SAF.Toolbox.RequestClient;
using SAF.Toolbox.Serialization;
using Xunit;
using static FileTransferGolden;

public class TextFileChunkSenderTests
{
    private readonly IRequestClient _requestClient = Substitute.For<IRequestClient>();

    [Fact]
    public void SendsText()
        => Assert.Equal(MessageFormats.Text, new TextFileChunkSender(_requestClient).Format);

    [Theory]
    [InlineData(FileReceiverStatus.Ok)]
    [InlineData(FileReceiverStatus.Failed)]
    public async Task SendAsync_SendsTheChunkInTheJsonPayload_AndReturnsTheReceiverStatus(FileReceiverStatus status)
    {
        var file = File;
        var chunk = new FileChunk { Index = 1, Data = ChunkData };
        _requestClient.SendRequestAwaitFirstAnswer<SendFileChunkRequest, SendFileChunkResponse>("topic",
                Arg.Is<SendFileChunkRequest>(r => r.File == file && r.FileChunk == chunk),
                Arg.Any<IJsonObjectConverter[]>(), "private/data/transfer/receipt", 1000)
            .Returns(new SendFileChunkResponse { Status = status });

        Assert.Equal(status, await new TextFileChunkSender(_requestClient).SendAsync("topic", file, chunk, 1000));
    }

    [Fact]
    public async Task SendAsync_ReturnsNull_WhenNoReplyArrives()
    {
        _requestClient.SendRequestAwaitFirstAnswer<SendFileChunkRequest, SendFileChunkResponse>(null!, null!, null!, null!, null!)
            .ReturnsForAnyArgs((SendFileChunkResponse?)null);

        Assert.Null(await new TextFileChunkSender(_requestClient).SendAsync("topic", File, new FileChunk { Index = 1, Data = ChunkData }, 1000));
    }
}
