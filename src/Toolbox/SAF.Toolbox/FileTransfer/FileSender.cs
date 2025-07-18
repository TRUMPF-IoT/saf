// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SAF.Toolbox.FileTransfer.Messages;
using SAF.Toolbox.RequestClient;

namespace SAF.Toolbox.FileTransfer;

internal class FileSender(
    ILogger<FileSender> log,
    IOptions<FileSenderOptions> options,
    IFileSystem fileSystem,
    IRequestClient requestClient) : IFileSender
{
    private readonly FileSenderOptions _options = options.Value;

    public Task<FileTransferStatus> SendAsync(string topic, string fullFilePath, uint timeoutMs)
        => SendAsync(topic, fullFilePath, timeoutMs, new Dictionary<string, string>());

    public async Task<FileTransferStatus> SendAsync(string topic, string fullFilePath, uint timeoutMs, IDictionary<string, string> properties)
    {
        try
        {
            var fi = fileSystem.FileInfo.New(fullFilePath);
            if (!CanBeSend(fi, out var status))
            {
                return status;
            }

            var totalChunks = (uint)Math.Ceiling((double)fi.Length / _options.MaxChunkSizeInBytes);

            var transportFile = new TransportFile
            {
                FileName = Path.GetFileName(fullFilePath),
                FileId = fi.GetFileId(_options.MaxChunkSizeInBytes),
                ContentHash = fi.GetContentHash(),
                ContentLength = fi.Length,
                ChunkSize = _options.MaxChunkSizeInBytes,
                TotalChunks = totalChunks,
                Properties = properties
            };

            var receiverState = await requestClient.GetReceiverStateAsync(topic, transportFile, _options);
            if (receiverState == null)
            {
                log.LogError("Failed to retrieve transmitted chunk information for file {FullFilePath} on topic {ReceiverTopic}", fullFilePath, topic);
                return FileTransferStatus.Error;
            }

            if (receiverState.FileExists || receiverState.TransmittedChunks.Count == totalChunks)
            {
                log.LogInformation("File {FullFilePath} already transferred on topic {ReceiverTopic}, skip sending and report success", fullFilePath, topic);
                return FileTransferStatus.Delivered;
            }

            log.LogInformation("Sending {FullFilePath} with {MissingChunks} of {TotalChunks} chunks to {ReceiverTopic}",
                fullFilePath, totalChunks - receiverState.TransmittedChunks.Count, totalChunks, topic);

            await using var fileStream = fi.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            
            var buffer = new byte[_options.MaxChunkSizeInBytes];
            for (uint chunk = 0; chunk < totalChunks; chunk++)
            {
                if(receiverState.TransmittedChunks.Contains(chunk))
                {
                    log.LogDebug("Chunk {ChunkIndex} of file {FullFilePath} on topic {ReceiverTopic} already transmitted, skipping", chunk, fullFilePath, topic);
                    continue;
                }

                var fileChunk = await ReadFileChunkAsync(chunk, fileStream, buffer);

                var transferStatus = await requestClient.SendFileChunkAsync(topic,
                    new SendFileChunkRequest { File = transportFile, FileChunk = fileChunk },
                    _options, timeoutMs);
                if(transferStatus != FileTransferStatus.Delivered)
                {
                    log.LogError("Failed to send chunk {ChunkIndex} of file {FullFilePath} to topic {ReceiverTopic}", chunk, fullFilePath, topic);
                    return transferStatus;
                }
            }

            return FileTransferStatus.Delivered;
        }
        catch (IOException e)
        {
            log.LogError(e, "Could not open file {FullFilePath}", fullFilePath);
            return FileTransferStatus.Error;
        }
        catch(Exception e)
        {
            log.LogError(e, "An unexpected error occurred while sending file {FullFilePath}", fullFilePath);
            return FileTransferStatus.Error;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // not required anymore, but kept for compatibility
        }
    }

    private static async Task<FileChunk> ReadFileChunkAsync(uint chunkIndex, FileSystemStream fileStream, byte[] buffer)
    {
        var bytesRead = await fileStream.ReadAsync(buffer, CancellationToken.None);
        if (bytesRead == 0)
        {
            throw new EndOfStreamException("Reached unexpected EOF");
        }

        if (bytesRead != buffer.Length)
        {
            Array.Resize(ref buffer, bytesRead);
        }

        return new FileChunk
        {
            Index = chunkIndex,
            Data = buffer
        };
    }

    private bool CanBeSend(IFileInfo fi, out FileTransferStatus status)
    {
        status = FileTransferStatus.Delivered;

        if (!fi.Exists)
        {
            log.LogError("File {FullFilePath} not found", fi.FullName);
            status = FileTransferStatus.FileNotFound;
            return false;
        }

        if (fi.Length == 0)
        {
            log.LogInformation("File {FullFilePath} has no content, skip sending and report success", fi.FullName);
            status = FileTransferStatus.Delivered;
            return false;
        }

        if (_options.MaxChunkSizeInBytes == 0)
        {
            log.LogError("Configured MaxChunkSizeInBytes is set to 0!");
            status = FileTransferStatus.Error;
            return false;
        }

        return true;
    }
}