// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.Tests.FileTransfer.Chunks;

using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SAF.Messaging.Contracts;
using SAF.Toolbox.FileTransfer;
using SAF.Toolbox.FileTransfer.Chunks;
using Xunit;
using static FileTransferGolden;

public class RetryingFileChunkSenderTests
{
    private readonly IFileChunkSender _inner = Substitute.For<IFileChunkSender>();
    private readonly FileChunk _chunk = new() { Index = 1, Data = ChunkData };

    private RetryingFileChunkSender CreateSender(int retryAttempts)
        => new(_inner, retryAttempts, _ => TimeSpan.Zero, NullLogger.Instance);

    [Theory]
    [InlineData(MessageFormats.Text, true)]
    [InlineData(MessageFormats.Binary, false)]
    public void TakesFormatAndCanSendFromTheSender(MessageFormats format, bool canSend)
    {
        _inner.Format.Returns(format);
        _inner.CanSend("topic").Returns(canSend);

        Assert.Equal(format, CreateSender(2).Format);
        Assert.Equal(canSend, CreateSender(2).CanSend("topic"));
    }

    [Fact]
    public async Task SendAsync_SendsOnce_WhenTheReceiverConfirms()
    {
        var file = File;
        _inner.SendAsync("topic", file, _chunk, 1000).Returns(FileReceiverStatus.Ok);

        Assert.Equal(FileReceiverStatus.Ok, await CreateSender(2).SendAsync("topic", file, _chunk, 1000));
        await _inner.ReceivedWithAnyArgs(1).SendAsync(default!, default!, default!, default);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task SendAsync_RetriesUpToTheGivenAttempts_AndReturnsTheLastStatus(int retryAttempts)
    {
        _inner.SendAsync(default!, default!, default!, default).ReturnsForAnyArgs((FileReceiverStatus?)null);

        Assert.Null(await CreateSender(retryAttempts).SendAsync("topic", File, _chunk, 1000));
        await _inner.ReceivedWithAnyArgs(retryAttempts + 1).SendAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task SendAsync_StopsRetrying_WhenTheReceiverConfirms()
    {
        _inner.SendAsync(default!, default!, default!, default).ReturnsForAnyArgs(FileReceiverStatus.Failed, FileReceiverStatus.Ok);

        Assert.Equal(FileReceiverStatus.Ok, await CreateSender(5).SendAsync("topic", File, _chunk, 1000));
        await _inner.ReceivedWithAnyArgs(2).SendAsync(default!, default!, default!, default);
    }
}
