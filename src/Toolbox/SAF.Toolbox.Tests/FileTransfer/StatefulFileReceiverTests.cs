// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SAF.Toolbox.FileTransfer;
using SAF.Toolbox.Serialization;
using System.IO.Abstractions.TestingHelpers;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;
using SAF.Toolbox.Heartbeat;
using Xunit;
using Xunit.Abstractions;

namespace SAF.Toolbox.Tests.FileTransfer;

public class StatefulFileReceiverTests
{
    private const int DefaultChunkSize = 1024 * 200;
    private static readonly string DefaultFolderPath = "testFolder";

    private readonly ILogger<StatefulFileReceiver> _logger;
    private readonly MockFileSystem _fileSystem = new();
    private readonly IHeartbeatPool _heartbeatPool = Substitute.For<IHeartbeatPool>();
    private readonly IOptions<FileReceiverOptions> _options = Options.Create(new FileReceiverOptions());

    private readonly string _defaultTestFilePath;

    public StatefulFileReceiverTests(ITestOutputHelper outputHelper)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddXunit(outputHelper, LogLevel.Trace).SetMinimumLevel(LogLevel.Warning));
        _logger = loggerFactory.CreateLogger<StatefulFileReceiver>();

        _defaultTestFilePath = _fileSystem.Path.Combine(DefaultFolderPath, "file.txt");
    }

    [Fact]
    public void GetState_ReturnsFileExists_WhenTargetFileExistsAndHashMatches()
    {
        _fileSystem.AddFile(_defaultTestFilePath, new MockFileData("content"));
        var fileInfo = _fileSystem.FileInfo.New(_defaultTestFilePath);

        var file = new TransportFile
        {
            FileName = "file.txt",
            FileId = fileInfo.GetFileId(DefaultChunkSize),
            ContentHash = fileInfo.GetContentHash(),
            ChunkSize = DefaultChunkSize,
            ContentLength = fileInfo.Length,
            TotalChunks = 1
        };

        using var receiver = new StatefulFileReceiver(_logger, _fileSystem, _options.Value, _heartbeatPool, DefaultFolderPath);

        BeforeFileReceivedEventArgs? beforeFileReceived = null;
        FileReceivedEventArgs? fileReceived = null;
        receiver.BeforeFileReceived += (_, bfr) => beforeFileReceived = bfr;
        receiver.FileReceived += (_, fr) => fileReceived = fr;

        var state = receiver.GetState(file);

        Assert.True(state.FileExists);
        Assert.NotNull(beforeFileReceived);
        Assert.NotNull(fileReceived);
        Assert.Equal(fileInfo.FullName, fileReceived.LocalFileFullName);
    }

    [Fact]
    public void GetState_ReturnsFileExists_WhenTargetFileExistsAndHashMatches_AndGeneratesNewFileForNoOverwrite()
    {
        _fileSystem.AddFile(_defaultTestFilePath, new MockFileData("content"));
        var fileInfo = _fileSystem.FileInfo.New(_defaultTestFilePath);

        var file = new TransportFile
        {
            FileName = "file.txt",
            FileId = fileInfo.GetFileId(DefaultChunkSize),
            ContentHash = fileInfo.GetContentHash(),
            ChunkSize = DefaultChunkSize,
            ContentLength = fileInfo.Length,
            TotalChunks = 1
        };

        using var receiver = new StatefulFileReceiver(_logger, _fileSystem, _options.Value, _heartbeatPool, DefaultFolderPath);

        BeforeFileReceivedEventArgs? beforeFileReceived = null;
        FileReceivedEventArgs? fileReceived = null;
        receiver.BeforeFileReceived += (_, bfr) =>
        {
            bfr.AllowOverwrite = false;
            beforeFileReceived = bfr;
        };
        receiver.FileReceived += (_, fr) => fileReceived = fr;

        var state = receiver.GetState(file);

        Assert.True(state.FileExists);
        Assert.NotNull(beforeFileReceived);
        Assert.NotNull(fileReceived);
        Assert.NotEqual(fileInfo.FullName, fileReceived.LocalFileFullName);

        var localFileInfo = _fileSystem.FileInfo.New(fileReceived.LocalFileFullName);
        Assert.True(localFileInfo.Exists);
        Assert.Equal(fileInfo.GetContentHash(), localFileInfo.GetContentHash());
        Assert.True(fileInfo.Exists);
    }

    [Fact]
    public void GetState_ReturnsEmptyChunks_WhenTempFileDoesNotExist()
    {
        _fileSystem.AddFile(_defaultTestFilePath, new MockFileData("content"));
        
        var fileInfo = _fileSystem.FileInfo.New(_defaultTestFilePath);
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

        using var receiver = new StatefulFileReceiver(_logger, _fileSystem, _options.Value, _heartbeatPool, DefaultFolderPath);

        var state = receiver.GetState(file);

        Assert.False(state.FileExists);
        Assert.Empty(state.TransmittedChunks);
    }

    [Fact]
    public void GetState_ReturnsEmptyChunks_WhenNoMetadataExist()
    {
        _fileSystem.AddFile(_defaultTestFilePath, new MockFileData("content"));
        
        var fileInfo = _fileSystem.FileInfo.New(_defaultTestFilePath);
        var file = new TransportFile
        {
            FileName = "file.txt",
            FileId = fileInfo.GetFileId(DefaultChunkSize),
            ContentHash = fileInfo.GetContentHash(),
            ChunkSize = DefaultChunkSize,
            ContentLength = fileInfo.Length,
            TotalChunks = 5
        };

        _fileSystem.AddFile(file.GetTempTargetFilePath(_fileSystem, DefaultFolderPath), new MockFileData("content"));
        fileInfo.Delete();

        using var receiver = new StatefulFileReceiver(_logger, _fileSystem, _options.Value, _heartbeatPool, DefaultFolderPath);

        var state = receiver.GetState(file);

        Assert.False(state.FileExists);
        Assert.Empty(state.TransmittedChunks);
    }

    [Fact]
    public void GetState_ReturnsTransmittedChunks_WhenMetadataExist()
    {
        _fileSystem.AddFile(_defaultTestFilePath, new MockFileData("content"));

        var fileInfo = _fileSystem.FileInfo.New(_defaultTestFilePath);
        var file = new TransportFile
        {
            FileName = "file.txt",
            FileId = fileInfo.GetFileId(DefaultChunkSize),
            ContentHash = fileInfo.GetContentHash(),
            ChunkSize = DefaultChunkSize,
            ContentLength = fileInfo.Length,
            TotalChunks = 5
        };

        _fileSystem.AddFile(file.GetTempTargetFilePath(_fileSystem, DefaultFolderPath), new MockFileData("content"));

        HashSet<uint> hashSet = [0, 1, 2];
        _fileSystem.AddFile(file.GetMetadataTargetFilePath(_fileSystem, DefaultFolderPath), new MockFileData(JsonSerializer.Serialize(new { ReceivedChunks = hashSet })));
        fileInfo.Delete();

        using var receiver = new StatefulFileReceiver(_logger, _fileSystem, _options.Value, _heartbeatPool, DefaultFolderPath);

        var state = receiver.GetState(file);

        Assert.False(state.FileExists);
        Assert.Equal(3, state.TransmittedChunks.Count);
    }

    [Fact]
    public void GetState_CompletesFileTransfer_WhenMetadataContainsAllChunks()
    {
        _fileSystem.AddFile(_defaultTestFilePath, new MockFileData("content"));

        var fileInfo = _fileSystem.FileInfo.New(_defaultTestFilePath);
        var file = new TransportFile
        {
            FileName = "file.txt",
            FileId = fileInfo.GetFileId(DefaultChunkSize),
            ContentHash = fileInfo.GetContentHash(),
            ChunkSize = DefaultChunkSize,
            ContentLength = fileInfo.Length,
            TotalChunks = 5
        };

        _fileSystem.AddFile(file.GetTempTargetFilePath(_fileSystem, DefaultFolderPath), new MockFileData("content"));

        HashSet<uint> hashSet = [0, 1, 2, 3, 4];
        _fileSystem.AddFile(file.GetMetadataTargetFilePath(_fileSystem, DefaultFolderPath), new MockFileData(JsonSerializer.Serialize(new { ReceivedChunks = hashSet })));
        fileInfo.Delete();

        using var receiver = new StatefulFileReceiver(_logger, _fileSystem, _options.Value, _heartbeatPool, DefaultFolderPath);

        BeforeFileReceivedEventArgs? beforeFileReceived = null;
        FileReceivedEventArgs? fileReceived = null;
        receiver.BeforeFileReceived += (_, bfr) => beforeFileReceived = bfr;
        receiver.FileReceived += (_, fr) => fileReceived = fr;
        var state = receiver.GetState(file);

        Assert.True(state.FileExists);
        Assert.Equal(5, state.TransmittedChunks.Count);
        Assert.False(_fileSystem.File.Exists(file.GetTempTargetFilePath(_fileSystem, DefaultFolderPath)));
        Assert.False(_fileSystem.File.Exists(file.GetMetadataTargetFilePath(_fileSystem, DefaultFolderPath)));
        Assert.True(_fileSystem.File.Exists(file.GetTargetFilePath(_fileSystem, DefaultFolderPath)));
        Assert.NotNull(beforeFileReceived);
        Assert.NotNull(fileReceived);
        Assert.Equal(_fileSystem.Path.GetFullPath(file.GetTargetFilePath(_fileSystem, DefaultFolderPath)), fileReceived.LocalFileFullName);
    }

    [Fact]
    public void GetState_CompletesFileTransfer_WhenMetadataContainsAllChunks_AndOverwritesExistingTargetFile()
    {
        _fileSystem.AddFile(_defaultTestFilePath, new MockFileData("content"));

        var fileInfo = _fileSystem.FileInfo.New(_defaultTestFilePath);
        var file = new TransportFile
        {
            FileName = "file.txt",
            FileId = fileInfo.GetFileId(DefaultChunkSize),
            ContentHash = fileInfo.GetContentHash(),
            ChunkSize = DefaultChunkSize,
            ContentLength = fileInfo.Length,
            TotalChunks = 5
        };

        _fileSystem.AddFile(file.GetTempTargetFilePath(_fileSystem, DefaultFolderPath), new MockFileData("content"));

        HashSet<uint> hashSet = [0, 1, 2, 3, 4];
        _fileSystem.AddFile(file.GetMetadataTargetFilePath(_fileSystem, DefaultFolderPath), new MockFileData(JsonSerializer.Serialize(new { ReceivedChunks = hashSet })));
        
        fileInfo.Delete();
        _fileSystem.AddFile(_defaultTestFilePath, new MockFileData("otherContent"));

        using var receiver = new StatefulFileReceiver(_logger, _fileSystem, _options.Value, _heartbeatPool, DefaultFolderPath);

        BeforeFileReceivedEventArgs? beforeFileReceived = null;
        FileReceivedEventArgs? fileReceived = null;
        receiver.BeforeFileReceived += (_, bfr) => beforeFileReceived = bfr;
        receiver.FileReceived += (_, fr) => fileReceived = fr;
        var state = receiver.GetState(file);

        Assert.True(state.FileExists);
        Assert.Equal(5, state.TransmittedChunks.Count);
        Assert.False(_fileSystem.File.Exists(file.GetTempTargetFilePath(_fileSystem, DefaultFolderPath)));
        Assert.False(_fileSystem.File.Exists(file.GetMetadataTargetFilePath(_fileSystem, DefaultFolderPath)));
        Assert.True(_fileSystem.File.Exists(file.GetTargetFilePath(_fileSystem, DefaultFolderPath)));
        Assert.NotNull(beforeFileReceived);
        Assert.NotNull(fileReceived);
        Assert.Equal(_fileSystem.Path.GetFullPath(file.GetTargetFilePath(_fileSystem, DefaultFolderPath)), fileReceived.LocalFileFullName);
    }

    [Fact]
    public void WriteFile_ReturnsOk_WhenChunkIsWritten()
    {
        _fileSystem.AddFile(_defaultTestFilePath, new MockFileData("content"));

        var fileInfo = _fileSystem.FileInfo.New(_defaultTestFilePath);
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
        var tempFilePath = file.GetTempTargetFilePath(_fileSystem, DefaultFolderPath);
        var metadataFilePath = file.GetMetadataTargetFilePath(_fileSystem, DefaultFolderPath);
        fileInfo.Delete();

        using var receiver = new StatefulFileReceiver(_logger, _fileSystem, _options.Value, _heartbeatPool, DefaultFolderPath);

        var status = receiver.WriteFile(file, chunk);

        Assert.Equal(FileReceiverStatus.Ok, status);
        Assert.True(_fileSystem.File.Exists(tempFilePath));
        Assert.True(_fileSystem.File.Exists(metadataFilePath));
    }

    [Fact]
    public void WriteFile_ReturnsOk_WhenChunkWasAlreadyReceived()
    {
        _fileSystem.AddFile(_defaultTestFilePath, new MockFileData("content"));

        var fileInfo = _fileSystem.FileInfo.New(_defaultTestFilePath);
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
            file.GetMetadataTargetFilePath(_fileSystem, DefaultFolderPath),
            new MockFileData(JsonSerializer.Serialize(new { ReceivedChunks = hashSet })));

        var chunk = new FileChunk { Index = 3, Data = _fileSystem.File.ReadAllBytes(fileInfo.FullName) };
        var tempFilePath = file.GetTempTargetFilePath(_fileSystem, DefaultFolderPath);
        fileInfo.Delete();

        using var receiver = new StatefulFileReceiver(_logger, _fileSystem, _options.Value, _heartbeatPool, DefaultFolderPath);

        var status = receiver.WriteFile(file, chunk);

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

        _fileSystem.AddFile(_defaultTestFilePath, new MockFileData(contentBytes));

        var totalChunks = (uint)Math.Ceiling((double)contentLength / DefaultChunkSize);
        var fileInfo = _fileSystem.FileInfo.New(_defaultTestFilePath);
        var file = new TransportFile
        {
            FileName = "file.txt",
            FileId = fileInfo.GetFileId(DefaultChunkSize),
            ContentHash = fileInfo.GetContentHash(),
            ChunkSize = DefaultChunkSize,
            ContentLength = fileInfo.Length,
            TotalChunks = totalChunks
        };

        var tempFilePath = file.GetTempTargetFilePath(_fileSystem, DefaultFolderPath);
        var metadataFilePath = file.GetMetadataTargetFilePath(_fileSystem, DefaultFolderPath);
        fileInfo.Delete();

        using var receiver = new StatefulFileReceiver(_logger, _fileSystem, _options.Value, _heartbeatPool, DefaultFolderPath);

        BeforeFileReceivedEventArgs? beforeFileReceived = null;
        FileReceivedEventArgs? fileReceived = null;
        receiver.BeforeFileReceived += (_, bfr) => beforeFileReceived = bfr;
        receiver.FileReceived += (_, fr) => fileReceived = fr;

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

            var status = receiver.WriteFile(file, fileChunk);
            
            Assert.Equal(FileReceiverStatus.Ok, status);
            Assert.True(_fileSystem.File.Exists(tempFilePath));
            Assert.True(_fileSystem.File.Exists(metadataFilePath));
            Assert.Null(beforeFileReceived);
            Assert.Null(fileReceived);
        }

        // Last chunk may be smaller than DefaultChunkSize
        var lastChunkSize = contentLength - lengthSent;
        var lastSpan = new ReadOnlySpan<byte>(contentBytes, lengthSent, lastChunkSize);

        var lastChunk = new FileChunk
        {
            Index = totalChunks - 1,
            Data = lastSpan.ToArray()
        };

        var lastStatus = receiver.WriteFile(file, lastChunk);
        Assert.Equal(FileReceiverStatus.Ok, lastStatus);

        Assert.False(_fileSystem.File.Exists(tempFilePath));
        Assert.False(_fileSystem.File.Exists(metadataFilePath));
        
        var transferredFile = _fileSystem.FileInfo.New(_defaultTestFilePath);
        Assert.True(transferredFile.Exists);
        Assert.Equal(file.ContentHash, transferredFile.GetContentHash());
        Assert.Equal(file.ContentLength, transferredFile.Length);
        Assert.NotNull(beforeFileReceived);
        Assert.NotNull(fileReceived);
        Assert.Equal(transferredFile.FullName, fileReceived.LocalFileFullName);
    }

    [Fact]
    public void WriteFile_ReturnsOk_AndStoresFileToPathSetInEventHandler()
    {
        const int contentLength = 4;
        var contentBytes = new byte[contentLength];
        var rand = new Random();
        rand.NextBytes(contentBytes);

        _fileSystem.AddFile(_defaultTestFilePath, new MockFileData(contentBytes));

        var fileInfo = _fileSystem.FileInfo.New(_defaultTestFilePath);
        var file = new TransportFile
        {
            FileName = "file.txt",
            FileId = fileInfo.GetFileId(DefaultChunkSize),
            ContentHash = fileInfo.GetContentHash(),
            ChunkSize = DefaultChunkSize,
            ContentLength = contentLength,
            TotalChunks = 1
        };

        var tempFilePath = file.GetTempTargetFilePath(_fileSystem, DefaultFolderPath);
        var metadataFilePath = file.GetMetadataTargetFilePath(_fileSystem, DefaultFolderPath);
        fileInfo.Delete();

        using var receiver = new StatefulFileReceiver(_logger, _fileSystem, _options.Value, _heartbeatPool, DefaultFolderPath);

        TargetFilePathResolvedEventArgs? targetFilePathResolvedReceived = null;
        FileReceivedEventArgs? fileReceived = null;
        receiver.TargetFilePathResolved += (_, r) =>
        {
            r.TargetFilePath = _fileSystem.Path.Combine(DefaultFolderPath, "custom", "changed.txt");
            targetFilePathResolvedReceived = r;
        };
        receiver.FileReceived += (_, fr) => fileReceived = fr;

        var lastChunk = new FileChunk
        {
            Index = 0,
            Data = contentBytes
        };

        var lastStatus = receiver.WriteFile(file, lastChunk);
        Assert.Equal(FileReceiverStatus.Ok, lastStatus);

        var expectedFilePath = _fileSystem.Path.GetFullPath(_fileSystem.Path.Combine(DefaultFolderPath, "custom", "changed.txt"));
        Assert.NotNull(targetFilePathResolvedReceived);
        Assert.NotNull(fileReceived);
        Assert.Equal(expectedFilePath, fileReceived.LocalFileFullName);

        Assert.False(_fileSystem.File.Exists(tempFilePath));
        Assert.False(_fileSystem.File.Exists(metadataFilePath));

        var transferredFile = _fileSystem.FileInfo.New(expectedFilePath);
        Assert.True(transferredFile.Exists);
        Assert.Equal(file.ContentHash, transferredFile.GetContentHash());
        Assert.Equal(file.ContentLength, transferredFile.Length);
        Assert.Equal(transferredFile.FullName, fileReceived.LocalFileFullName);
    }

    [Fact]
    public void WriteFile_ReturnsFailed_OnException()
    {
        var fileSystem = Substitute.For<IFileSystem>();

        _fileSystem.AddFile(_defaultTestFilePath, new MockFileData("content"));

        var fileInfo = _fileSystem.FileInfo.New(_defaultTestFilePath);
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
        using var receiver = new StatefulFileReceiver(_logger, fileSystem, _options.Value, _heartbeatPool, DefaultFolderPath);

        var chunk = new FileChunk { Index = 0, Data = [1] };
        var status = receiver.WriteFile(file, chunk);
        
        Assert.Equal(FileReceiverStatus.Failed, status);
    }

    [Fact]
    public void StatefulReceiver_PerformsCleanup_AfterStateFileExpiration()
    {
        _fileSystem.AddFile(_defaultTestFilePath, new MockFileData("content"));

        _fileSystem.AddFile(_fileSystem.Path.Combine(DefaultFolderPath, "file.fileId1.temp"), new MockFileData("content"));
        _fileSystem.AddFile(_fileSystem.Path.Combine(DefaultFolderPath, "file.fileId1.meta"), new MockFileData("content"));

        _fileSystem.AddFile(_fileSystem.Path.Combine(DefaultFolderPath, "subdir", "file.fileId2.temp"), new MockFileData("content"));
        _fileSystem.AddFile(_fileSystem.Path.Combine(DefaultFolderPath, "subdir", "file.fileId2.meta"), new MockFileData("content"));

        var heartbeat = Substitute.For<IHeartbeat>();
        _heartbeatPool.GetOrCreateHeartbeat(Arg.Any<int>()).Returns(heartbeat);

        var timeProvider = Substitute.For<TimeProvider>();
        using var receiver = new StatefulFileReceiver(_logger, _fileSystem, _options.Value, _heartbeatPool, DefaultFolderPath, timeProvider);

        timeProvider.GetUtcNow().Returns(DateTime.UtcNow.AddHours(_options.Value.StateExpirationAfterHours + 1));
        heartbeat.Beat += Raise.EventWith(new HeartbeatEventArgs(1, 1));

        Assert.False(_fileSystem.File.Exists(_fileSystem.Path.Combine(DefaultFolderPath, "file.fileId1.temp")));
        Assert.False(_fileSystem.File.Exists(_fileSystem.Path.Combine(DefaultFolderPath, "file.fileId1.meta")));
        Assert.False(_fileSystem.File.Exists(_fileSystem.Path.Combine(DefaultFolderPath, "subdir", "file.fileId2.temp")));
        Assert.False(_fileSystem.File.Exists(_fileSystem.Path.Combine(DefaultFolderPath, "subdir", "file.fileId2.meta")));
    }

    [Fact]
    public void StatefulReceiver_PerformsNoCleanup_WhenStateFileNotExpired()
    {
        _fileSystem.AddFile(_defaultTestFilePath, new MockFileData("content"));

        _fileSystem.AddFile(_fileSystem.Path.Combine(DefaultFolderPath, "file.fileId1.temp"), new MockFileData("content"));
        _fileSystem.AddFile(_fileSystem.Path.Combine(DefaultFolderPath, "file.fileId1.meta"), new MockFileData("content"));

        _fileSystem.AddFile(_fileSystem.Path.Combine(DefaultFolderPath, "subdir", "file.fileId2.temp"), new MockFileData("content"));
        _fileSystem.AddFile(_fileSystem.Path.Combine(DefaultFolderPath, "subdir", "file.fileId2.meta"), new MockFileData("content"));

        var heartbeat = Substitute.For<IHeartbeat>();
        _heartbeatPool.GetOrCreateHeartbeat(Arg.Any<int>()).Returns(heartbeat);

        var timeProvider = Substitute.For<TimeProvider>();
        using var receiver = new StatefulFileReceiver(_logger, _fileSystem, _options.Value, _heartbeatPool, DefaultFolderPath, timeProvider);

        timeProvider.GetUtcNow().Returns(DateTime.UtcNow.AddHours(1));
        heartbeat.Beat += Raise.EventWith(new HeartbeatEventArgs(1, 1));

        Assert.True(_fileSystem.File.Exists(_fileSystem.Path.Combine(DefaultFolderPath, "file.fileId1.temp")));
        Assert.True(_fileSystem.File.Exists(_fileSystem.Path.Combine(DefaultFolderPath, "file.fileId1.meta")));
        Assert.True(_fileSystem.File.Exists(_fileSystem.Path.Combine(DefaultFolderPath, "subdir", "file.fileId2.temp")));
        Assert.True(_fileSystem.File.Exists(_fileSystem.Path.Combine(DefaultFolderPath, "subdir", "file.fileId2.meta")));
    }
}