// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SAF.Toolbox.FileTransfer;
using SAF.Toolbox.FileTransfer.Messages;
using SAF.Toolbox.RequestClient;
using Testably.Abstractions.Testing;
using System.IO.Abstractions;
using Xunit;

namespace SAF.Toolbox.Tests.FileTransfer;

public class FileSenderTests
{
    private readonly ILogger<FileSender> _logger;
    private readonly MockFileSystem _fileSystem = new();
    private readonly IRequestClient _requestClient = Substitute.For<IRequestClient>();
    private readonly IOptions<FileSenderOptions> _defaultOptions = Options.Create(new FileSenderOptions());

    public FileSenderTests(ITestOutputHelper outputHelper)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddXUnit(outputHelper).SetMinimumLevel(LogLevel.Warning));
        _logger = loggerFactory.CreateLogger<FileSender>();
    }

    private void AddFile(string path, byte[] content)
        => _fileSystem.Initialize().WithFile(path).Which(f => f.HasBytesContent(content));

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
        AddFile("empty.txt", []);

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
        AddFile("test.txt", [0, 1, 2, 3]);

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
        AddFile("file.txt", [0, 1, 2, 3]);

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
        AddFile("file.txt", [0, 1, 2, 3]);

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
        AddFile("file.txt", [0, 1, 2, 3]);

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

        AddFile("file.txt", buffer);

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

    [Fact]
    public async Task SendAsync_ReturnsDelivered_AndSkipsAlreadySentChunks()
    {
        const int contentLength = 200 * 1024 * 5;

        var buffer = new byte[contentLength];
        
        AddFile("file.txt", buffer);

        _requestClient.SendRequestAwaitFirstAnswer<GetReceiverStateRequest, GetReceiverStateResponse>(
                null!, null!, null!, null!, null!)
            .ReturnsForAnyArgs(new GetReceiverStateResponse { State = new FileReceiverState { TransmittedChunks = [1, 3] } });
        var expectedSendCalls = (int)Math.Ceiling((double)contentLength / _defaultOptions.Value.MaxChunkSizeInBytes) - 2;

        var sentContentLength = 0;
        _requestClient.SendRequestAwaitFirstAnswer<SendFileChunkRequest, SendFileChunkResponse>(
                null!, null!, null!, null!, null!)
            .ReturnsForAnyArgs(ci =>
            {
                var request = ci.Arg<SendFileChunkRequest>();
                sentContentLength += request.FileChunk!.Data.Length;
                return new SendFileChunkResponse { Status = FileReceiverStatus.Ok };
            });

        var sender = new FileSender(_logger, _defaultOptions, _fileSystem, _requestClient);

        var result = await sender.SendAsync("topic", "file.txt", 1000);

        Assert.Equal(FileTransferStatus.Delivered, result);
        await _requestClient.ReceivedWithAnyArgs(expectedSendCalls).
            SendRequestAwaitFirstAnswer<SendFileChunkRequest, SendFileChunkResponse>(null!, null!, null!, null!, null!);
        Assert.Equal(contentLength - 2 * 200 * 1024, sentContentLength);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task SendAsync_WithMissingAnswerReturnsTimedOutOk(int expectedCalls)
    {
        var buffer = new byte[1024];

        AddFile("file.txt", buffer);

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

    [Theory]
    [InlineData(new uint[] { 0, 1 })]
    [InlineData(new uint[] { 1, 3 })]
    [InlineData(new uint[] { 0, 2 })]
    public async Task SendAsync_SendsTheDataOfEachChunk_WhenResumingATransfer(uint[] transmittedChunks)
    {
        var content = Enumerable.Range(0, 14).Select(i => (byte)i).ToArray();
        AddFile("file.txt", content);

        _requestClient.SendRequestAwaitFirstAnswer<GetReceiverStateRequest, GetReceiverStateResponse>(
                null!, null!, null!, null!, null!)
            .ReturnsForAnyArgs(new GetReceiverStateResponse { State = new FileReceiverState { TransmittedChunks = [.. transmittedChunks] } });
        var sentChunks = CaptureSentChunks();

        var options = Options.Create(new FileSenderOptions { MaxChunkSizeInBytes = 4 });
        var sender = new FileSender(_logger, options, _fileSystem, _requestClient);

        var result = await sender.SendAsync("topic", "file.txt", 1000);

        Assert.Equal(FileTransferStatus.Delivered, result);
        Assert.Equal(new uint[] { 0, 1, 2, 3 }.Except(transmittedChunks), sentChunks.Select(c => c.Index));
        Assert.All(sentChunks, c => Assert.Equal(content.Skip((int)c.Index * 4).Take(4), c.Data));
    }

    [Fact]
    public async Task SendAsync_SendsWholeChunks_WhenTheStreamReturnsFewerBytesThanRequested()
    {
        var content = Enumerable.Range(0, 10).Select(i => (byte)i).ToArray();
        var fileSystem = Substitute.For<IFileSystem>();
        var fileInfo = Substitute.For<IFileInfo>();
        fileSystem.FileInfo.New("file.bin").Returns(fileInfo);
        fileInfo.Exists.Returns(true);
        fileInfo.Name.Returns("file.bin");
        fileInfo.Length.Returns(content.Length);
        fileInfo.OpenRead().Returns(_ => new TricklingStream(content));
        fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read).Returns(_ => new TricklingStream(content));

        _requestClient.SendRequestAwaitFirstAnswer<GetReceiverStateRequest, GetReceiverStateResponse>(
                null!, null!, null!, null!, null!)
            .ReturnsForAnyArgs(new GetReceiverStateResponse { State = new FileReceiverState() });
        var sentChunks = CaptureSentChunks();

        var options = Options.Create(new FileSenderOptions { MaxChunkSizeInBytes = 4 });
        var sender = new FileSender(_logger, options, fileSystem, _requestClient);

        var result = await sender.SendAsync("topic", "file.bin", 1000);

        Assert.Equal(FileTransferStatus.Delivered, result);
        Assert.Equal([[0, 1, 2, 3], [4, 5, 6, 7], [8, 9]], sentChunks.Select(c => c.Data));
    }

    private List<FileChunk> CaptureSentChunks()
    {
        var sentChunks = new List<FileChunk>();
        _requestClient.SendRequestAwaitFirstAnswer<SendFileChunkRequest, SendFileChunkResponse>(
                null!, null!, null!, null!, null!)
            .ReturnsForAnyArgs(ci =>
            {
                var chunk = ci.Arg<SendFileChunkRequest>().FileChunk!;
                sentChunks.Add(new FileChunk { Index = chunk.Index, Data = chunk.Data.ToArray() });
                return new SendFileChunkResponse { Status = FileReceiverStatus.Ok };
            });
        return sentChunks;
    }

    // Returns at most two bytes per read, which streams are allowed to do.
    private sealed class TricklingStream(byte[] content) : FileSystemStream(new MemoryStream(content), "file.bin", false)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => base.ReadAsync(buffer[..Math.Min(2, buffer.Length)], cancellationToken);
    }
}