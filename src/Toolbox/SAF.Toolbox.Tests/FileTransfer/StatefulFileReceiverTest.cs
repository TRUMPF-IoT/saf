// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SAF.Toolbox.FileTransfer;
using SAF.Toolbox.Serialization;
using System.IO.Abstractions.TestingHelpers;
using NSubstitute.ExceptionExtensions;
using Xunit;
using Xunit.Abstractions;

namespace SAF.Toolbox.Tests.FileTransfer;

public class StatefulFileReceiverTest
{
    private const int DefaultChunkSize = 1024 * 200;

    private readonly ILogger<StatefulFileReceiver> _logger;
    private readonly MockFileSystem _fileSystem = new();

    public StatefulFileReceiverTest(ITestOutputHelper outputHelper)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddXunit(outputHelper, LogLevel.Trace).SetMinimumLevel(LogLevel.Warning));
        _logger = loggerFactory.CreateLogger<StatefulFileReceiver>();
    }

    [Fact]
    public void GetState_ReturnsFileExists_WhenTargetFileExistsAndHashMatches()
    {
        _fileSystem.AddFile("file.txt", new MockFileData("content"));
        var fileInfo = _fileSystem.FileInfo.New("file.txt");

        var file = new TransportFile
        {
            FileName = "file.txt",
            FileId = fileInfo.GetFileId(DefaultChunkSize),
            ContentHash = fileInfo.GetContentHash(),
            ChunkSize = DefaultChunkSize,
            ContentLength = fileInfo.Length,
            TotalChunks = 1
        };

        var receiver = new StatefulFileReceiver(_logger, _fileSystem);

        var state = receiver.GetState(string.Empty, file);

        Assert.True(state.FileExists);
    }

    [Fact]
    public void GetState_ReturnsEmptyChunks_WhenTempFileDoesNotExist()
    {
        _fileSystem.AddFile("file.txt", new MockFileData("content"));
        
        var fileInfo = _fileSystem.FileInfo.New("file.txt");
        var file = new TransportFile
        {
            FileName = "file.txt",
            FileId = fileInfo.GetFileId(DefaultChunkSize),
            ContentHash = fileInfo.GetContentHash(),
            ChunkSize = DefaultChunkSize,
            ContentLength = fileInfo.Length,
            TotalChunks = 1
        };
        fileInfo.Delete();

        var receiver = new StatefulFileReceiver(_logger, _fileSystem);

        var state = receiver.GetState(string.Empty, file);

        Assert.False(state.FileExists);
        Assert.Empty(state.TransmittedChunks);
    }

    [Fact]
    public void GetState_ReturnsEmptyChunks_WhenNoMetadataExist()
    {
        _fileSystem.AddFile("file.txt", new MockFileData("content"));
        
        var fileInfo = _fileSystem.FileInfo.New("file.txt");
        var file = new TransportFile
        {
            FileName = "file.txt",
            FileId = fileInfo.GetFileId(DefaultChunkSize),
            ContentHash = fileInfo.GetContentHash(),
            ChunkSize = DefaultChunkSize,
            ContentLength = fileInfo.Length,
            TotalChunks = 5
        };

        _fileSystem.AddFile(file.GetTempTargetFilePath(string.Empty), new MockFileData("content"));
        fileInfo.Delete();

        var receiver = new StatefulFileReceiver(_logger, _fileSystem);

        var state = receiver.GetState(string.Empty, file);

        Assert.False(state.FileExists);
        Assert.Empty(state.TransmittedChunks);
    }

    [Fact]
    public void GetState_ReturnsTransmittedChunks_WhenMetadataExist()
    {
        _fileSystem.AddFile("file.txt", new MockFileData("content"));

        var fileInfo = _fileSystem.FileInfo.New("file.txt");
        var file = new TransportFile
        {
            FileName = "file.txt",
            FileId = fileInfo.GetFileId(DefaultChunkSize),
            ContentHash = fileInfo.GetContentHash(),
            ChunkSize = DefaultChunkSize,
            ContentLength = fileInfo.Length,
            TotalChunks = 5
        };

        _fileSystem.AddFile(file.GetTempTargetFilePath(string.Empty), new MockFileData("content"));

        HashSet<uint> hashSet = [0, 1, 2];
        _fileSystem.AddFile(file.GetMetadataTargetFilePath(string.Empty), new MockFileData(JsonSerializer.Serialize(new { ReceivedChunks = hashSet })));
        fileInfo.Delete();

        var receiver = new StatefulFileReceiver(_logger, _fileSystem);

        var state = receiver.GetState(string.Empty, file);

        Assert.False(state.FileExists);
        Assert.Equal(3, state.TransmittedChunks.Count);
    }

    [Fact]
    public void GetState_CompletesFileTransfer_WhenMetadataContainsAllChunks()
    {
        _fileSystem.AddFile("file.txt", new MockFileData("content"));

        var fileInfo = _fileSystem.FileInfo.New("file.txt");
        var file = new TransportFile
        {
            FileName = "file.txt",
            FileId = fileInfo.GetFileId(DefaultChunkSize),
            ContentHash = fileInfo.GetContentHash(),
            ChunkSize = DefaultChunkSize,
            ContentLength = fileInfo.Length,
            TotalChunks = 5
        };

        _fileSystem.AddFile(file.GetTempTargetFilePath(string.Empty), new MockFileData("content"));

        HashSet<uint> hashSet = [0, 1, 2, 3, 4];
        _fileSystem.AddFile(file.GetMetadataTargetFilePath(string.Empty), new MockFileData(JsonSerializer.Serialize(new { ReceivedChunks = hashSet })));
        fileInfo.Delete();

        var receiver = new StatefulFileReceiver(_logger, _fileSystem);

        string? receivedFileName = null;
        receiver.FileReceived += fn => receivedFileName = fn;
        var state = receiver.GetState(string.Empty, file);

        Assert.True(state.FileExists);
        Assert.Equal(5, state.TransmittedChunks.Count);
        Assert.False(_fileSystem.File.Exists(file.GetTempTargetFilePath(string.Empty)));
        Assert.False(_fileSystem.File.Exists(file.GetMetadataTargetFilePath(string.Empty)));
        Assert.True(_fileSystem.File.Exists(file.GetTargetFilePath(string.Empty)));
        Assert.NotNull(receivedFileName);
        Assert.Equal(_fileSystem.Path.GetFullPath(file.GetTargetFilePath(string.Empty)), receivedFileName);
    }

    [Fact]
    public void WriteFile_ReturnsOk_WhenChunkIsWritten()
    {
        _fileSystem.AddFile("file.txt", new MockFileData("content"));

        var fileInfo = _fileSystem.FileInfo.New("file.txt");
        var file = new TransportFile
        {
            FileName = "file.txt",
            FileId = fileInfo.GetFileId(DefaultChunkSize),
            ContentHash = fileInfo.GetContentHash(),
            ChunkSize = DefaultChunkSize,
            ContentLength = fileInfo.Length,
            TotalChunks = 5
        };

        var chunk = new FileChunk { Index = 3, Data = _fileSystem.File.ReadAllBytes(fileInfo.FullName) };
        var tempFilePath = file.GetTempTargetFilePath(string.Empty);
        var metadataFilePath = file.GetMetadataTargetFilePath(string.Empty);
        fileInfo.Delete();

        var receiver = new StatefulFileReceiver(_logger, _fileSystem);

        var status = receiver.WriteFile(string.Empty, file, chunk);

        Assert.Equal(FileReceiverStatus.Ok, status);
        Assert.True(_fileSystem.File.Exists(tempFilePath));
        Assert.True(_fileSystem.File.Exists(metadataFilePath));
    }

    [Fact]
    public void WriteFile_ReturnsOk_WhenChunkWasAlreadyReceived()
    {
        _fileSystem.AddFile("file.txt", new MockFileData("content"));

        var fileInfo = _fileSystem.FileInfo.New("file.txt");
        var file = new TransportFile
        {
            FileName = "file.txt",
            FileId = fileInfo.GetFileId(DefaultChunkSize),
            ContentHash = fileInfo.GetContentHash(),
            ChunkSize = DefaultChunkSize,
            ContentLength = fileInfo.Length,
            TotalChunks = 5
        };

        HashSet<uint> hashSet = [0, 3, 4];
        _fileSystem.AddFile(
            file.GetMetadataTargetFilePath(string.Empty),
            new MockFileData(JsonSerializer.Serialize(new { ReceivedChunks = hashSet })));

        var chunk = new FileChunk { Index = 3, Data = _fileSystem.File.ReadAllBytes(fileInfo.FullName) };
        var tempFilePath = file.GetTempTargetFilePath(string.Empty);
        fileInfo.Delete();

        var receiver = new StatefulFileReceiver(_logger, _fileSystem);

        var status = receiver.WriteFile(string.Empty, file, chunk);

        Assert.Equal(FileReceiverStatus.Ok, status);
        Assert.False(_fileSystem.File.Exists(tempFilePath));
    }

    [Theory]
    [InlineData(1)] // 1 byte
    [InlineData(1024)] // 1 kByte
    [InlineData(1024 * 3)]  // 3 kByte
    [InlineData(1024 * 1024)] // 1 MByte
    [InlineData(1024 * 1024 * 3)]  // 3 MByte
    [InlineData(DefaultChunkSize - 1)] // exact chunk size - 1
    [InlineData(DefaultChunkSize)] // exact chunk size
    [InlineData(DefaultChunkSize + 1)] // exact chunk size + 1
    [InlineData(DefaultChunkSize * 3 - 1)] // multiple of chunk size - 1
    [InlineData(DefaultChunkSize * 3)] // multiple of chunk size
    [InlineData(DefaultChunkSize * 3 + 1)] // multiple of chunk size + 1
    public void WriteFile_CompletesTransfer_WhenAllChunksWereReceived(int contentLength)
    {
        var contentBytes = new byte[contentLength];
        var rand = new Random();
        rand.NextBytes(contentBytes);

        _fileSystem.AddFile("file.txt", new MockFileData(contentBytes));

        var totalChunks = (uint)Math.Ceiling((double)contentLength / DefaultChunkSize);
        var fileInfo = _fileSystem.FileInfo.New("file.txt");
        var file = new TransportFile
        {
            FileName = "file.txt",
            FileId = fileInfo.GetFileId(DefaultChunkSize),
            ContentHash = fileInfo.GetContentHash(),
            ChunkSize = DefaultChunkSize,
            ContentLength = fileInfo.Length,
            TotalChunks = totalChunks
        };

        var tempFilePath = file.GetTempTargetFilePath(string.Empty);
        var metadataFilePath = file.GetMetadataTargetFilePath(string.Empty);
        fileInfo.Delete();

        var receiver = new StatefulFileReceiver(_logger, _fileSystem);

        string? receivedFile = null;
        receiver.FileReceived += f => receivedFile = f;

        var lengthSent = 0;
        for (uint chunk = 0; chunk < totalChunks - 1; chunk++)
        {
            var span = new ReadOnlySpan<byte>(contentBytes, (int)(chunk * DefaultChunkSize),  DefaultChunkSize);
            
            var fileChunk = new FileChunk
            {
                Index = chunk,
                Data = span.ToArray()
            };
            lengthSent += fileChunk.Data.Length;

            var status = receiver.WriteFile(string.Empty, file, fileChunk);
            
            Assert.Equal(FileReceiverStatus.Ok, status);
            Assert.True(_fileSystem.File.Exists(tempFilePath));
            Assert.True(_fileSystem.File.Exists(metadataFilePath));
            Assert.Null(receivedFile);
        }

        // Last chunk may be smaller than DefaultChunkSize
        var lastChunkSize = contentLength - lengthSent;
        var lastSpan = new ReadOnlySpan<byte>(contentBytes, lengthSent, lastChunkSize);

        var lastChunk = new FileChunk
        {
            Index = totalChunks - 1,
            Data = lastSpan.ToArray()
        };

        var lastStatus = receiver.WriteFile(string.Empty, file, lastChunk);
        Assert.Equal(FileReceiverStatus.Ok, lastStatus);

        Assert.False(_fileSystem.File.Exists(tempFilePath));
        Assert.False(_fileSystem.File.Exists(metadataFilePath));
        
        var transferredFile = _fileSystem.FileInfo.New("file.txt");
        Assert.True(transferredFile.Exists);
        Assert.Equal(file.ContentHash, transferredFile.GetContentHash());
        Assert.Equal(file.ContentLength, transferredFile.Length);
        Assert.NotNull(receivedFile);
        Assert.Equal(transferredFile.FullName, receivedFile);
    }

    [Fact]
    public void WriteFile_ReturnsFailed_OnException()
    {
        var fileSystem = Substitute.For<IFileSystem>();

        _fileSystem.AddFile("file.txt", new MockFileData("content"));

        var fileInfo = _fileSystem.FileInfo.New("file.txt");
        var file = new TransportFile
        {
            FileName = "file.txt",
            FileId = fileInfo.GetFileId(DefaultChunkSize),
            ContentHash = fileInfo.GetContentHash(),
            ChunkSize = DefaultChunkSize,
            ContentLength = fileInfo.Length,
            TotalChunks = 5
        };
        fileInfo.Delete();

        fileSystem.FileInfo.New(Arg.Any<string>()).Throws(new Exception("fail"));
        var receiver = new StatefulFileReceiver(_logger, fileSystem);

        var chunk = new FileChunk { Index = 0, Data = [1] };
        var status = receiver.WriteFile(string.Empty, file, chunk);
        
        Assert.Equal(FileReceiverStatus.Failed, status);
    }
}