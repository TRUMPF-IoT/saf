// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SAF.Messaging.Contracts;
using SAF.Toolbox.FileTransfer;
using SAF.Toolbox.FileTransfer.Chunks;
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
    // Reports no publishable formats unless a test says otherwise, so the sender falls back to text chunks.
    private readonly IMessagingInfrastructure _messaging = Substitute.For<IMessagingInfrastructure>();
    private readonly IOptions<FileSenderOptions> _defaultOptions = Options.Create(new FileSenderOptions());

    public FileSenderTests(ITestOutputHelper outputHelper)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddXUnit(outputHelper).SetMinimumLevel(LogLevel.Warning));
        _logger = loggerFactory.CreateLogger<FileSender>();
    }

    private void AddFile(string path, byte[] content)
        => _fileSystem.Initialize().WithFile(path).Which(f => f.HasBytesContent(content));

    private FileSender CreateSender(IOptions<FileSenderOptions> options, IFileSystem fileSystem)
        => ServiceCollectionExtensions.CreateFileSender(_logger, options, fileSystem, _requestClient, _messaging);

    [Fact]
    public async Task SendAsync_ReturnsFileNotFound_WhenFileDoesNotExist()
    {
        var sender = CreateSender(_defaultOptions, _fileSystem);

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

        var sender = CreateSender(_defaultOptions, _fileSystem);

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

        var sender = CreateSender(options, _fileSystem);

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

        var sender = CreateSender(_defaultOptions, fileSystem);

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

        var sender = CreateSender(_defaultOptions, fileSystem);

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

        var sender = CreateSender(_defaultOptions, _fileSystem);

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

        var sender = CreateSender(_defaultOptions, _fileSystem);

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

        var sender = CreateSender(_defaultOptions, _fileSystem);

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

        var sender = CreateSender(_defaultOptions, _fileSystem);

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

        var sender = CreateSender(_defaultOptions, _fileSystem);

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
        var sender = CreateSender(options, _fileSystem);

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
        var sender = CreateSender(options, _fileSystem);

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
        var sender = CreateSender(options, fileSystem);

        var result = await sender.SendAsync("topic", "file.bin", 1000);

        Assert.Equal(FileTransferStatus.Delivered, result);
        Assert.Equal([[0, 1, 2, 3], [4, 5, 6, 7], [8, 9]], sentChunks.Select(c => c.Data));
    }

    [Theory]
    [InlineData(null, MessageFormats.Text | MessageFormats.Binary, MessageFormats.Text)]
    [InlineData(MessageFormats.Text, MessageFormats.Text | MessageFormats.Binary, MessageFormats.Text)]
    [InlineData(MessageFormats.Text | MessageFormats.Binary, MessageFormats.Text | MessageFormats.Binary, MessageFormats.Binary)]
    [InlineData(MessageFormats.Text | MessageFormats.Binary, MessageFormats.Text, MessageFormats.Text)]
    [InlineData(MessageFormats.Text | MessageFormats.Binary, MessageFormats.None, MessageFormats.Text)]
    public async Task SendAsync_SendsInTheFirstFormatTheReceiverAcceptsAndTheMessagingPublishes(
        MessageFormats? acceptedChunkFormats, MessageFormats publishableFormats, MessageFormats expected)
    {
        AddFile("file.txt", [0, 1, 2, 3]);
        ReturnReceiverState(new FileReceiverState(), acceptedChunkFormats);
        _messaging.GetPublishableFormats("topic").Returns(publishableFormats);
        var binary = CreateChunkSender(MessageFormats.Binary);
        var text = CreateChunkSender(MessageFormats.Text);
        var sender = new FileSender(_logger, _defaultOptions, _fileSystem, _requestClient, _messaging, [binary, text]);

        Assert.Equal(FileTransferStatus.Delivered, await sender.SendAsync("topic", "file.txt", 1000));

        var (used, unused) = expected == MessageFormats.Binary ? (binary, text) : (text, binary);
        await used.Received(1).SendAsync("topic", Arg.Is<TransportFile>(f => f.FileName == "file.txt"), Arg.Is<FileChunk>(c => c.Index == 0), 1000);
        await unused.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task SendAsync_ReadsEachChunkIntoItsOwnBuffer()
    {
        AddFile("file.txt", Enumerable.Range(0, 10).Select(i => (byte)i).ToArray());
        ReturnReceiverState(new FileReceiverState(), MessageFormats.Text | MessageFormats.Binary);
        _messaging.GetPublishableFormats("topic").Returns(MessageFormats.Text | MessageFormats.Binary);
        var sentChunks = new List<FileChunk>();
        var binary = CreateChunkSender(MessageFormats.Binary);
        binary.SendAsync(default!, default!, default!, default).ReturnsForAnyArgs(ci =>
        {
            sentChunks.Add(ci.Arg<FileChunk>());
            return FileReceiverStatus.Ok;
        });
        var options = Options.Create(new FileSenderOptions { MaxChunkSizeInBytes = 4 });
        var sender = new FileSender(_logger, options, _fileSystem, _requestClient, _messaging, [binary]);

        await sender.SendAsync("topic", "file.txt", 1000);

        // A binary payload travels by reference, so reading a chunk must not change an earlier one.
        Assert.Equal([[0, 1, 2, 3], [4, 5, 6, 7], [8, 9]], sentChunks.Select(c => c.Data));
    }

    [Theory]
    [InlineData(null, FileTransferStatus.TimedOut)]
    [InlineData(FileReceiverStatus.Failed, FileTransferStatus.Error)]
    [InlineData(FileReceiverStatus.ContentMismatch, FileTransferStatus.Error)]
    public async Task SendAsync_StopsAtAChunkTheReceiverDoesNotConfirm(FileReceiverStatus? receiverStatus, FileTransferStatus expected)
    {
        AddFile("file.txt", new byte[10]);
        ReturnReceiverState(new FileReceiverState(), null);
        var text = CreateChunkSender(MessageFormats.Text);
        text.SendAsync(default!, default!, default!, default).ReturnsForAnyArgs(receiverStatus);
        var options = Options.Create(new FileSenderOptions { MaxChunkSizeInBytes = 4 });
        var sender = new FileSender(_logger, options, _fileSystem, _requestClient, _messaging, [text]);

        Assert.Equal(expected, await sender.SendAsync("topic", "file.txt", 1000));
        await text.ReceivedWithAnyArgs(1).SendAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task SendAsync_LogsAContentMismatch_InsteadOfAFailedChunk()
    {
        AddFile("file.txt", new byte[10]);
        ReturnReceiverState(new FileReceiverState(), null);
        var text = CreateChunkSender(MessageFormats.Text);
        text.SendAsync(default!, default!, default!, default).ReturnsForAnyArgs(FileReceiverStatus.ContentMismatch);
        var logger = Substitute.For<ILogger<FileSender>>();
        var sender = new FileSender(logger, _defaultOptions, _fileSystem, _requestClient, _messaging, [text]);

        Assert.Equal(FileTransferStatus.Error, await sender.SendAsync("topic", "file.txt", 1000));
        var error = Assert.Single(logger.ReceivedCalls(),
            call => call.GetMethodInfo().Name == nameof(ILogger.Log) && Equals(call.GetArguments()[0], LogLevel.Error));
        Assert.Contains("does not match the sent content", error.GetArguments()[2]!.ToString());
    }

    private void ReturnReceiverState(FileReceiverState state, MessageFormats? acceptedChunkFormats)
        => _requestClient.SendRequestAwaitFirstAnswer<GetReceiverStateRequest, GetReceiverStateResponse>(
                null!, null!, null!, null!, null!)
            .ReturnsForAnyArgs(new GetReceiverStateResponse { State = state, AcceptedChunkFormats = acceptedChunkFormats });

    private static IFileChunkSender CreateChunkSender(MessageFormats format)
    {
        var sender = Substitute.For<IFileChunkSender>();
        sender.Format.Returns(format);
        sender.SendAsync(default!, default!, default!, default).ReturnsForAnyArgs(FileReceiverStatus.Ok);
        return sender;
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