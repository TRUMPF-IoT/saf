// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Toolbox.Heartbeat;

namespace SAF.Toolbox.FileTransfer;

using Microsoft.Extensions.Logging;
using Serialization;
using System;
using System.IO;
using System.IO.Abstractions;

public class StatefulFileReceiver : IStatefulFileReceiver
{
    private readonly ILogger<StatefulFileReceiver> _log;
    private readonly IFileSystem _fileSystem;
    private readonly FileReceiverOptions _options;
    private readonly string _folderPath;
    private readonly TimeProvider _timeProvider;
    private readonly IHeartbeat? _heartbeat;

    private static readonly FileTransferLockManager _lockManager = new();

    public StatefulFileReceiver(ILogger<StatefulFileReceiver> log,
        IFileSystem fileSystem,
        FileReceiverOptions options,
        IHeartbeatPool heartbeatPool,
        string folderPath)
    : this(log, fileSystem, options, heartbeatPool, folderPath, TimeProvider.System)
    {

    }

    internal StatefulFileReceiver(ILogger<StatefulFileReceiver> log,
        IFileSystem fileSystem,
        FileReceiverOptions options,
        IHeartbeatPool heartbeatPool,
        string folderPath,
        TimeProvider timeProvider)
    {
        _log = log;
        _fileSystem = fileSystem;
        _options = options;
        _folderPath = fileSystem.Path.GetFullPath(folderPath);
        _timeProvider = timeProvider;

        if (options.StateExpirationAfterHours != 0)
        {
            _log.LogDebug("Registering heartbeat for file receiver state cleanup");

            _heartbeat = heartbeatPool.GetOrCreateHeartbeat(60 * 60 * 1000);
            _heartbeat.Beat += OnStateCleanupHeartbeat;
        }
    }

    public FileReceiverState GetState(TransportFile file)
    {
        using var _ = _lockManager.Acquire(file.FileId);
            
        var targetFileInfo = _fileSystem.FileInfo.New(file.GetTargetFilePath(_fileSystem, _folderPath));
        if (targetFileInfo.Exists && file.ContentLength == targetFileInfo.Length && file.ContentHash == targetFileInfo.GetContentHash())
        {
            _log.LogDebug(
                "File {FileName} already exists with matching content hash, signal sender to skip transfer.",
                targetFileInfo.FullName);

            CompleteFileTransfer(file, file.GetTargetFilePath(_fileSystem, _folderPath));
            return new FileReceiverState {FileExists = true};
        }

        var tempTargetFileInfo = _fileSystem.FileInfo.New(file.GetTempTargetFilePath(_fileSystem, _folderPath));
        if (!tempTargetFileInfo.Exists)
        {
            _log.LogDebug("Temporary file {FileName} does not exist, signal sender to transfer all chunks.", tempTargetFileInfo.FullName);
            return new FileReceiverState();
        }

        var metadata = ReadFileMetadata(file.GetMetadataTargetFilePath(_fileSystem, _folderPath));
        if (metadata.ReceivedChunks.Count == file.TotalChunks)
        {
            _log.LogDebug("All chunks of file {FileName} already received, signal sender to skip transfer.", file.FileName);
            
            CompleteFileTransfer(file);
            return new FileReceiverState {FileExists = true, TransmittedChunks = metadata.ReceivedChunks};
        }

        return new FileReceiverState {TransmittedChunks = metadata.ReceivedChunks};
    }

    public FileReceiverStatus WriteFile(TransportFile file, FileChunk fileChunk)
    {
        try
        {
            using var _ = _lockManager.Acquire(file.FileId);
            
            var metadata = ReadFileMetadata(file.GetMetadataTargetFilePath(_fileSystem, _folderPath));
            if (metadata.ReceivedChunks.Contains(fileChunk.Index))
            {
                _log.LogDebug("Chunk {ChunkIndex} of file {FileName} already received, skipping.", fileChunk.Index, file.FileName);
            }
            else
            {
                var directoryInfo = _fileSystem.DirectoryInfo.New(_folderPath);
                if (!directoryInfo.Exists)
                {
                    directoryInfo.Create();
                }

                var tempTargetFile = _fileSystem.FileInfo.New(file.GetTempTargetFilePath(_fileSystem, _folderPath));
                WriteChunk(tempTargetFile, fileChunk, file.ChunkSize);

                metadata.ReceivedChunks.Add(fileChunk.Index);
                SaveFileMetadata(file.GetMetadataTargetFilePath(_fileSystem, _folderPath), metadata);
            }

            if (metadata.ReceivedChunks.Count == file.TotalChunks)
            {
                CompleteFileTransfer(file);
            }

            return FileReceiverStatus.Ok;
        }
        catch (Exception e)
        {
            _log.LogError(e, "Failed to write file");
            return FileReceiverStatus.Failed;
        }
    }

    public event EventHandler<BeforeFileReceivedEventArgs>? BeforeFileReceived;
    public event EventHandler<FileReceivedEventArgs>? FileReceived;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        if(_heartbeat != null)
        {
            _heartbeat.Beat -= OnStateCleanupHeartbeat;
        }
    }

    private void CompleteFileTransfer(TransportFile file)
        => CompleteFileTransfer(file, file.GetTempTargetFilePath(_fileSystem, _folderPath));

    private void CompleteFileTransfer(TransportFile file, string sourceFilePath)
    {
        var targetFilePath = file.GetTargetFilePath(_fileSystem, _folderPath);
        var beforeEventArgs = new BeforeFileReceivedEventArgs(file, targetFilePath);
        BeforeFileReceived?.Invoke(this, beforeEventArgs);

        targetFilePath = _fileSystem.Path.GetFullPath(beforeEventArgs.TargetFilePath);
        if (!beforeEventArgs.AllowOverwrite)
        {
            targetFilePath = GenerateUniqueTargetFilePath(targetFilePath);
        }

        if (sourceFilePath != targetFilePath)
        {
            var targetDirectory = _fileSystem.Path.GetDirectoryName(targetFilePath);
            if(targetDirectory != null && !_fileSystem.Directory.Exists(targetDirectory))
            {
                _fileSystem.Directory.CreateDirectory(targetDirectory);
            }
            _fileSystem.File.Copy(sourceFilePath, targetFilePath);
        }

        if (_fileSystem.File.Exists(file.GetTempTargetFilePath(_fileSystem, _folderPath)))
        {
            _fileSystem.File.Delete(file.GetTempTargetFilePath(_fileSystem, _folderPath));
        }
        if (_fileSystem.File.Exists(file.GetMetadataTargetFilePath(_fileSystem, _folderPath)))
        {
            _fileSystem.File.Delete(file.GetMetadataTargetFilePath(_fileSystem, _folderPath));
        }

        FileReceived?.Invoke(this, new FileReceivedEventArgs(file, targetFilePath));
    }

    private sealed class FileMetadata
    {
        public HashSet<uint> ReceivedChunks { get; set; } = [];
    }

    private FileMetadata ReadFileMetadata(string metadataFilePath)
    {
        var metadataFileInfo = _fileSystem.FileInfo.New(metadataFilePath);
        if (!metadataFileInfo.Exists)
        {
            _log.LogDebug("Metadata file {FileName} does not exist, signal sender to transfer all chunks.", metadataFilePath);
            return new FileMetadata();
        }

        var metadataContent = _fileSystem.File.ReadAllText(metadataFilePath);
        return JsonSerializer.Deserialize<FileMetadata>(metadataContent) ?? new FileMetadata();
    }

    private void SaveFileMetadata(string metadataFilePath, FileMetadata metadata)
    {
        var metadataContent = JsonSerializer.Serialize(metadata);
        _fileSystem.File.WriteAllText(metadataFilePath, metadataContent);
    }

    private static void WriteChunk(IFileInfo tempTargetFile, FileChunk fileChunk, uint chunkSize)
    {
        using var tempFileStream = tempTargetFile.Open(FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        tempFileStream.Seek(fileChunk.Index * chunkSize, SeekOrigin.Begin);
        tempFileStream.Write(fileChunk.Data);
    }

    private string GenerateUniqueTargetFilePath(string filePath)
    {
        var directory = _fileSystem.Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory)) return filePath;

        var fileName = _fileSystem.Path.GetFileName(filePath);
        var name = _fileSystem.Path.GetFileNameWithoutExtension(fileName);
        var ext = _fileSystem.Path.GetExtension(fileName);
        var n = 0;
        while (_fileSystem.File.Exists(filePath))
        {
            var pattern = $"{name}({++n}){ext}";
            filePath = _fileSystem.Path.Combine(directory, pattern);
        }

        return filePath;
    }

    private void OnStateCleanupHeartbeat(object? sender, HeartbeatEventArgs e)
    {
        _log.LogDebug("Cleaning up file receiver states");

        try
        {
            var tempFiles = _fileSystem.Directory.GetFiles(_folderPath, "*.temp", new EnumerationOptions { RecurseSubdirectories = true });
            var metadataFiles = _fileSystem.Directory.GetFiles(_folderPath, "*.meta", new EnumerationOptions { RecurseSubdirectories = true });

            foreach (var fileInfo in tempFiles.Concat(metadataFiles).Select(fn => _fileSystem.FileInfo.New(fn)))
            {
                try
                {
                    if (fileInfo.LastWriteTimeUtc <
                        _timeProvider.GetUtcNow() - TimeSpan.FromHours(_options.StateExpirationAfterHours))
                    {
                        _log.LogInformation("Deleting state file '{StateFileFullName}' as it expired after {StateExpirationAfterHours} hours.",
                            fileInfo.FullName, _options.StateExpirationAfterHours);
                        fileInfo.Delete();
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to delete state file '{StateFileFullName}'.", fileInfo.FullName);
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to delete file receiver states in folder {FileReceiverFolder}", _folderPath);
        }
    }
}