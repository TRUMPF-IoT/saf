// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SAF.Toolbox.FileTransfer;
using SAF.Toolbox.FileTransfer.Messages;
using SAF.Toolbox.RequestClient;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;
using Xunit.Abstractions;

namespace SAF.Toolbox.Tests.FileTransfer;

public class FileSenderTests
{
    private readonly ILogger<FileSender> _logger;
    private readonly MockFileSystem _fileSystem = new();
    private readonly IRequestClient _requestClient = Substitute.For<IRequestClient>();
    private readonly IOptions<FileSenderOptions> _defaultOptions = Options.Create(new FileSenderOptions());

    public FileSenderTests(ITestOutputHelper outputHelper)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddXunit(outputHelper, LogLevel.Trace).SetMinimumLevel(LogLevel.Warning));
        _logger = loggerFactory.CreateLogger<FileSender>();
    }

    [Fact]
    public async Task SendAsync_ReturnsFileNotFound_WhenFileDoesNotExist()
    {
        var sender = new FileSender(_logger, _defaultOptions, _fileSystem, _requestClient);

        var result = await sender.SendAsync("topic", "nofile.txt", 1000);

        Assert.Equal(FileTransferStatus.FileNotFound, result);
        await _requestClient.DidNotReceiveWithAnyArgs()
            .SendRequestAwaitFirstAnswer<GetReceiverStateRequest, GetReceiverStateResponse>(
                null!, null!, null!, null!, null!);
    }

    [Fact]
    public async Task SendAsync_ReturnsDelivered_WhenFileIsEmpty()
    {
        _fileSystem.AddFile("empty.txt", new MockFileData([]));

        var sender = new FileSender(_logger, _defaultOptions, _fileSystem, _requestClient);

        var result = await sender.SendAsync("topic", "empty.txt", 1000);

        Assert.Equal(FileTransferStatus.Delivered, result);
        await _requestClient.DidNotReceiveWithAnyArgs()
            .SendRequestAwaitFirstAnswer<GetReceiverStateRequest, GetReceiverStateResponse>(
                null!, null!, null!, null!, null!);
    }

    [Fact]
    public async Task SendAsync_ReturnsError_WhenMaxChunkSizeIsZero()
    {
        var options = Options.Create(new FileSenderOptions { MaxChunkSizeInBytes = 0 });
        _fileSystem.AddFile("test.txt", new MockFileData([0, 1, 2, 3]));

        var sender = new FileSender(_logger, options, _fileSystem, _requestClient);

        var result = await sender.SendAsync("topic", "test.txt", 1000);

        Assert.Equal(FileTransferStatus.Error, result);
        await _requestClient.DidNotReceiveWithAnyArgs()
            .SendRequestAwaitFirstAnswer<GetReceiverStateRequest, GetReceiverStateResponse>(
                null!, null!, null!, null!, null!);
    }

    [Fact]
    public async Task SendAsync_ReturnsError_WhenIOExceptionIsThrown()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var fileInfoFactory = Substitute.For<IFileInfoFactory>();

        fileSystem.FileInfo.Returns(fileInfoFactory);
        fileInfoFactory.New("file.txt").Throws(new IOException("File access error"));

        var sender = new FileSender(_logger, _defaultOptions, fileSystem, _requestClient);

        var result = await sender.SendAsync("topic", "file.txt", 1000);

        Assert.Equal(FileTransferStatus.Error, result);
    }

    [Fact]
    public async Task SendAsync_ReturnsError_WhenExceptionIsThrown()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var fileInfoFactory = Substitute.For<IFileInfoFactory>();

        fileSystem.FileInfo.Returns(fileInfoFactory);
        fileInfoFactory.New("file.txt").Throws(new Exception("File access error"));

        var sender = new FileSender(_logger, _defaultOptions, fileSystem, _requestClient);

        var result = await sender.SendAsync("topic", "file.txt", 1000);

        Assert.Equal(FileTransferStatus.Error, result);
    }

    [Fact]
    public async Task SendAsync_ReturnsError_WhenReceiverStateIsNull()
    {
        _fileSystem.AddFile("file.txt", new MockFileData([0, 1, 2, 3]));

        _requestClient.SendRequestAwaitFirstAnswer<GetReceiverStateRequest, GetReceiverStateResponse>(
                null!, null!, null!, null!, null!)
            .ReturnsForAnyArgs((GetReceiverStateResponse?)null);

        var sender = new FileSender(_logger, _defaultOptions, _fileSystem, _requestClient);

        var result = await sender.SendAsync("topic", "file.txt", 1000);

        Assert.Equal(FileTransferStatus.Error, result);
    }

    [Fact]
    public async Task SendAsync_ReturnsDelivered_WhenReceiverStateIndicatesFileExists()
    {
        _fileSystem.AddFile("file.txt", new MockFileData([0, 1, 2, 3]));

        _requestClient.SendRequestAwaitFirstAnswer<GetReceiverStateRequest, GetReceiverStateResponse>(
                null!, null!, null!, null!, null!)
            .ReturnsForAnyArgs(new GetReceiverStateResponse { State = new FileReceiverState { FileExists = true } });

        var sender = new FileSender(_logger, _defaultOptions, _fileSystem, _requestClient);

        var result = await sender.SendAsync("topic", "file.txt", 1000);

        Assert.Equal(FileTransferStatus.Delivered, result);
    }

    [Fact]
    public async Task SendAsync_ReturnsDelivered_WhenReceiverStateIndicatesAllChunksTransmitted()
    {
        _fileSystem.AddFile("file.txt", new MockFileData([0, 1, 2, 3]));

        _requestClient.SendRequestAwaitFirstAnswer<GetReceiverStateRequest, GetReceiverStateResponse>(
                null!, null!, null!, null!, null!)
            .ReturnsForAnyArgs(new GetReceiverStateResponse { State = new FileReceiverState { TransmittedChunks = [0] } });

        var sender = new FileSender(_logger, _defaultOptions, _fileSystem, _requestClient);

        var result = await sender.SendAsync("topic", "file.txt", 1000);

        Assert.Equal(FileTransferStatus.Delivered, result);
    }

    [Theory]
    [InlineData(1)] // 1 byte
    [InlineData(1024)] // 1 kByte
    [InlineData(1024 * 3)] // 3 kByte
    [InlineData(1024 * 1024)] // 1 MByte
    [InlineData(1024 * 1024 * 3)] // 3 MByte
    [InlineData(1024 * 200 - 1)] // exact chunk size - 1
    [InlineData(1024 * 200)] // exact chunk size
    [InlineData(1024 * 200 + 1)] // exact chunk size + 1
    [InlineData(1024 * 200 * 3 - 1)] // multiple of chunk size - 1
    [InlineData(1024 * 200 * 3)] // multiple of chunk size
    [InlineData(1024 * 200 * 3 + 1)] // multiple of chunk size + 1
    [InlineData(134217728)] // 128 MByte
    public async Task SendAsync_ReturnsDelivered_WhenAllChunksWereSent(int contentLength)
    {
        var buffer = new byte[contentLength];
        var expectedSendCalls = (int)Math.Ceiling((double)contentLength / _defaultOptions.Value.MaxChunkSizeInBytes);

        _fileSystem.AddFile("file.txt", new MockFileData(buffer));

        _requestClient.SendRequestAwaitFirstAnswer<GetReceiverStateRequest, GetReceiverStateResponse>(
                null!, null!, null!, null!, null!)
            .ReturnsForAnyArgs(new GetReceiverStateResponse { State = new FileReceiverState() });

        var sentContentLength = 0;
        _requestClient.SendRequestAwaitFirstAnswer<SendFileChunkRequest, SendFileChunkResponse>(
                null!, null!, null!, null!, null!)
            .ReturnsForAnyArgs(ci =>
            {
                var request = ci.Arg<SendFileChunkRequest>();
                sentContentLength += request.FileChunk!.Data.Length;
                return new SendFileChunkResponse {Status = FileReceiverStatus.Ok};
            });

        var sender = new FileSender(_logger, _defaultOptions, _fileSystem, _requestClient);

        var result = await sender.SendAsync("topic", "file.txt", 1000);

        Assert.Equal(FileTransferStatus.Delivered, result);
        await _requestClient.ReceivedWithAnyArgs(expectedSendCalls).
            SendRequestAwaitFirstAnswer<SendFileChunkRequest, SendFileChunkResponse>(null!, null!, null!, null!, null!);
        Assert.Equal(contentLength, sentContentLength);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task SendAsync_WithMissingAnswerReturnsTimedOutOk(int expectedCalls)
    {
        var buffer = new byte[1024];

        _fileSystem.AddFile("file.txt", new MockFileData(buffer));

        _requestClient.SendRequestAwaitFirstAnswer<GetReceiverStateRequest, GetReceiverStateResponse>(
                null!, null!, null!, null!, null!)
            .ReturnsForAnyArgs(new GetReceiverStateResponse { State = new FileReceiverState() });

        _requestClient.SendRequestAwaitFirstAnswer<SendFileChunkRequest, SendFileChunkResponse>(
                null!, null!, null!, null!, null!)
            .ReturnsForAnyArgs((SendFileChunkResponse?)null);

        var options = Options.Create(new FileSenderOptions {RetryAttemptsForFailedChunks = expectedCalls - 1});
        var sender = new FileSender(_logger, options, _fileSystem, _requestClient);

        var result = await sender.SendAsync("topic", "file.txt", 1000);

        Assert.Equal(FileTransferStatus.TimedOut, result);
        await _requestClient.ReceivedWithAnyArgs(expectedCalls).
            SendRequestAwaitFirstAnswer<SendFileChunkRequest, SendFileChunkResponse>(null!, null!, null!, null!, null!);
    }
}