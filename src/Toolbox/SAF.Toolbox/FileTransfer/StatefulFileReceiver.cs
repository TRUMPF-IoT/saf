using SAF.Toolbox.Serialization;
using System.IO.Abstractions;

namespace SAF.Toolbox.FileTransfer;

using Microsoft.Extensions.Logging;
using System;
using System.Runtime.CompilerServices;

public class StatefulFileReceiver(ILogger<StatefulFileReceiver> log, IFileSystem fileSystem) : IStatefulFileReceiver
{
    private readonly ConditionalWeakTable<string, object> _locks = new();

    public event Action<string>? FileReceived;

    public FileReceiverState GetState(string folderPath, TransportFile file)
    {
        var targetFileInfo = fileSystem.FileInfo.New(file.GetTargetFilePath(folderPath));
        if (targetFileInfo.Exists)
        {
            if(file.ContentHash == targetFileInfo.GetContentHash())
            {
                log.LogDebug("File {FileName} already exists with matching content hash, signal sender to skip transfer.", targetFileInfo.Name);
                return new FileReceiverState { FileExists = true };
            }
        }

        var tempTargetFileInfo = fileSystem.FileInfo.New(file.GetTempTargetFilePath(folderPath));
        if(!tempTargetFileInfo.Exists)
        {
            log.LogDebug("Temporary file {FileName} does not exist, signal sender to transfer all chunks.", tempTargetFileInfo.Name);
            return new FileReceiverState();
        }

        var metadata = ReadFileMetadata(file.GetMetadataTargetFilePath(folderPath));
        return new FileReceiverState {TransmittedChunks = metadata.ReceivedChunks};
    }

    public FileReceiverStatus WriteFile(string folderPath, TransportFile file, FileChunk fileChunk)
    {
        try
        {
            lock (GetFileTransferLock(file.FileId))
            {
                var metadata = ReadFileMetadata(file.GetMetadataTargetFilePath(folderPath));
                if (metadata.ReceivedChunks.Contains(fileChunk.Index))
                {
                    log.LogDebug("Chunk {ChunkIndex} of file {FileName} already received, skipping.", fileChunk.Index,
                        file.FileName);
                    return FileReceiverStatus.Ok;
                }

                var tempTargetFile = fileSystem.FileInfo.New(file.GetTempTargetFilePath(folderPath));
                WriteChunk(tempTargetFile, fileChunk, file.ChunkSize);

                metadata.ReceivedChunks.Add(fileChunk.Index);
                SaveFileMetadata(file.GetMetadataTargetFilePath(folderPath), metadata);

                if (metadata.ReceivedChunks.Count == file.TotalChunks)
                {
                    var targetFileInfo = fileSystem.FileInfo.New(file.GetTargetFilePath(folderPath));
                    fileSystem.File.Move(tempTargetFile.FullName, targetFileInfo.FullName, true);
                    fileSystem.File.Delete(file.GetMetadataTargetFilePath(folderPath));

                    FileReceived?.Invoke(targetFileInfo.FullName);
                }

                return FileReceiverStatus.Ok;
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Failed to write file");
            return FileReceiverStatus.Failed;
        }
    }

    private class FileMetadata
    {
        public HashSet<uint> ReceivedChunks { get; set; } = [];
    }

    private FileMetadata ReadFileMetadata(string metadataFilePath)
    {
        var metadataFileInfo = fileSystem.FileInfo.New(metadataFilePath);
        if (!metadataFileInfo.Exists)
        {
            log.LogDebug("Metadata file {FileName} does not exist, signal sender to transfer all chunks.", metadataFilePath);
            return new FileMetadata();
        }

        var metadataContent = fileSystem.File.ReadAllText(metadataFilePath);
        return JsonSerializer.Deserialize<FileMetadata>(metadataContent) ?? new FileMetadata();
    }

    private void SaveFileMetadata(string metadataFilePath, FileMetadata metadata)
    {
        var metadataContent = JsonSerializer.Serialize(metadata);
        fileSystem.File.WriteAllText(metadataFilePath, metadataContent);
    }

    private static void WriteChunk(IFileInfo tempTargetFile, FileChunk fileChunk, uint chunkSize)
    {
        using var tempFileStream = tempTargetFile.Open(FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        tempFileStream.Seek(fileChunk.Index * chunkSize, SeekOrigin.Begin);
        tempFileStream.Write(fileChunk.Data);
    }

    private object GetFileTransferLock(string fileId)
        => _locks.GetValue(string.Intern(fileId), _ => new object());
}