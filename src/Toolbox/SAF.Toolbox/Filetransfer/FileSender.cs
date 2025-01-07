// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.Filetransfer;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SAF.Common;
using SAF.Toolbox.Serialization;
using System.Collections.Concurrent;
using System.IO.MemoryMappedFiles;

internal class FileSender : IFileSender
{
    private sealed class SendRequest
    {
        public Action<Message> ReceiptAction { get; set; } = default!;
    }

    private readonly CancellationTokenSource _cancelTokenSource = new();
    private readonly IMessagingInfrastructure _messaging;
    private readonly ILogger<FileSender> _log;
    private readonly FileSenderConfiguration _options;

    private readonly ConcurrentDictionary<string, SendRequest> _sendRequests = new();
    private readonly object _subscription;

    internal const long MaxChunkSize = 204801; // roughly adding up to ~200KB

    private Guid Id { get; } = Guid.NewGuid();
    private string ReplyToPrefix => $"private/data/transfer/receipt/{Id:N}";

    private readonly object _syncTransferId = new();
    private long _uniqueTransferId;

    public ulong Timeout { get; set; } = 10_000; // 10 sec.

    public FileSender(IMessagingInfrastructure messaging, ILogger<FileSender>? log, IOptions<FileSenderConfiguration> options)
    {
        _messaging = messaging ?? throw new ArgumentNullException(nameof(messaging));
        _log = log ?? NullLogger<FileSender>.Instance;
        _options = options.Value;

        _subscription = _messaging.Subscribe($"{ReplyToPrefix}/*", msg =>
        {
            _log.LogDebug($"Received FileTransfer response for {msg.Topic}: payload='{msg.Payload}");
            if (_sendRequests.TryRemove(msg.Topic, out var request))
            {
                request.ReceiptAction(msg);
            }
            else
            {
                _log.LogInformation($"Lately received FileTransfer response '{msg.Payload}, {msg.Topic}'");
            }
        });
    }

    public Task<FileTransferStatus> Send(string topic, string fileName, string mimeType, Stream stream)
    {
        return Send(topic, fileName, mimeType, stream, Timeout);
    }

    public Task<FileTransferStatus> Send(string topic, string fileName, string mimeType, Stream stream, ulong timeoutMs)
    {
        var transFile = new TransportFile(fileName, mimeType);
        transFile.ReadFrom(stream);
        return Send(topic, transFile, timeoutMs);
    }

    public Task<FileTransferStatus> Send(string topic, TransportFile file)
    {
        return Send(topic, file, Timeout);
    }

    public async Task<FileTransferStatus> Send(string topic, TransportFile file, ulong timeoutMs)
    {
        if (string.IsNullOrEmpty(topic)) throw new ArgumentException("Topic must not be empty", nameof(topic));

        // Publish with reply-to and wait for response ...
        FileTransferStatus result;
        var replyTo = $"{ReplyToPrefix}/{Guid.NewGuid():N}";
        using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cancelTokenSource.Token))
        {
            var tcs = new TaskCompletionSource<FileTransferStatus>();
            linkedCts.Token.Register(() => tcs.TrySetCanceled());

            try
            {
                var request = new SendRequest
                {
                    ReceiptAction = message =>
                    {
                        tcs.TrySetResult((message.Payload ?? string.Empty).Equals("OK", StringComparison.OrdinalIgnoreCase)
                            ? FileTransferStatus.Delivered
                            : FileTransferStatus.Error);
                    }
                };
                _sendRequests.TryAdd(replyTo, request);

                // Send
                SendTransportFile(topic, file, replyTo);

                var waitResult = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromMilliseconds(timeoutMs), linkedCts.Token));
                if (waitResult.IsCanceled || waitResult.IsFaulted || waitResult.Status != TaskStatus.RanToCompletion)
                {
                    result = FileTransferStatus.Error;
                    _log.LogInformation("Cancelled transfer of file {fileName}. {replyTo}", file.Name, replyTo);
                }
                else
                {
                    result = waitResult == tcs.Task ? tcs.Task.Result : FileTransferStatus.TimedOut;
                    if(result != FileTransferStatus.Delivered)
                        _log.LogWarning("Transfer of file {fileName} timed out. {replyTo}", file.Name, replyTo);
                }
            }
            catch (OperationCanceledException)
            {
                result = FileTransferStatus.Error;
                _log.LogInformation("Cancelled transfer of file {fileName}. {replyTo}", file.Name, replyTo);
            }
            finally
            {
                _sendRequests.TryRemove(replyTo, out _);
            }
        }

        return result;
    }

    public async Task<FileTransferStatus> SendInChunks(string topic, string filePath, IDictionary<string, string>? properties = null)
    {
        return await SendInChunks(topic, filePath, Timeout, properties);
    }

    public async Task<FileTransferStatus> SendInChunks(string topic, string filePath, ulong timeoutMs, IDictionary<string, string>? properties = null)
    {
        try
        {
            var fi = new FileInfo(filePath);
            if (fi.Length == 0)
            {
                _log.LogError($"File is empty: {filePath}");
                return FileTransferStatus.Delivered;
            }

            var uniqueTransferId = GenerateUniqueTransferId();
            var length = fi.Length;
            var chunkSize = length < MaxChunkSize ? length : MaxChunkSize;
            var buffer = new byte[chunkSize];
            var pos = 0L;
            var offset = 0L;
            var n = 0;

            FileTransferStatus status;
            using (var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read))
            {
                // Write chunks 
                for (; pos < length - chunkSize; pos += chunkSize, n += 1)
                {
                    await ReadChunkFromFile(mmf, offset, buffer);
                    status = await RetryAsync(
                        action: () => TransferFileChunk(topic, filePath, properties, offset, buffer, length, n, false,
                            uniqueTransferId, timeoutMs),
                        isDesiredResult: s => s == FileTransferStatus.Delivered,
                        intervalFactory: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                        retryAttempts: _options.RetryAttemptsForFailedChunks);
                    if (status != FileTransferStatus.Delivered)
                    {
                        _log.LogError($"File {filePath} could not be sent. Status: {status}, chunk no. {n}");
                        return status;
                    }

                    offset += chunkSize;
                    await Task.Delay(20, _cancelTokenSource.Token);
                }

                // Read last chunk
                buffer = new byte[length - pos];
                await ReadChunkFromFile(mmf, offset, buffer);
            }

            // Send last chunk 
            status = await RetryAsync(
                action: () => TransferFileChunk(topic, filePath, properties, offset, buffer, length, n, true,
                    uniqueTransferId, timeoutMs),
                isDesiredResult: s => s == FileTransferStatus.Delivered,
                intervalFactory: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                retryAttempts: _options.RetryAttemptsForFailedChunks);
            if (status != FileTransferStatus.Delivered)
            {
                _log.LogError($"File {filePath} could not be sent. Status: {status}, last chunk no. {n}");
            }
            return status;
        }
        catch(IOException e)
        {
            _log.LogError($"Could not open file {filePath}", e);
            return FileTransferStatus.Error;
        }
    }

    private static async Task<TResult> RetryAsync<TResult>(
        Func<Task<TResult>> action,
        Predicate<TResult> isDesiredResult,
        Func<int, TimeSpan> intervalFactory,
        int retryAttempts = 0)
    {
        // Perform the action once before retrying
        var result = await action();

        if (isDesiredResult(result))
        {
            return result;
        }

        for (var attempted = 0; attempted < retryAttempts; attempted++)
        {
            result = await action();

            if (isDesiredResult(result))
            {
                return result;
            }

            var retryInterval = intervalFactory(attempted);
            // Wait before retrying
            await Task.Delay(retryInterval);
        }

        // Return the last result, even if it is not the desired one
        return result;
    }

    private long GenerateUniqueTransferId()
    {
        lock (_syncTransferId)
        {
            var id = ++_uniqueTransferId;
            if (id != 0) return id;
            return ++_uniqueTransferId;
        }
    }

    private async Task ReadChunkFromFile(MemoryMappedFile mmf, long offset, byte[] buffer)
    {
        await using var accessor = mmf.CreateViewStream(offset, 0, MemoryMappedFileAccess.Read);
        _ = await accessor.ReadAsync(buffer, 0, buffer.Length);
    }

    private async Task<FileTransferStatus> TransferFileChunk(
        string topic,
        string filePath,
        IDictionary<string, string>? properties,
        long offset,
        byte[] buffer,
        long fileSize,
        long chunkNumber,
        bool isLast,
        long transferId,
        ulong timeoutMs)
    {
        // Generate Transport File
        var file = GenerateTransportFile(filePath, properties ?? new Dictionary<string, string>(), buffer, offset, fileSize, chunkNumber, isLast, transferId);
        // Send
        return await Send(topic, file, timeoutMs);
    }

    private static TransportFile GenerateTransportFile(string filePath, IDictionary<string, string> properties, byte[] buffer, long offset, long fileSize, long chunkNumber, bool isLast, long transferId)
    {
        var name = Path.GetFileName(filePath);
        properties[FileTransferIdentifiers.Chunked] = $"{true}";
        properties[FileTransferIdentifiers.LastChunk] = $"{isLast}";
        properties[FileTransferIdentifiers.ChunkNumber] = $"{chunkNumber}";
        properties[FileTransferIdentifiers.FileSize] = $"{fileSize}";
        properties[FileTransferIdentifiers.ChunkSize] = $"{buffer.Length}";
        properties[FileTransferIdentifiers.ChunkOffset] = $"{offset}";
        properties[FileTransferIdentifiers.TransferId] = $"{transferId}";

        var transFile = new TransportFile(name, properties: properties);
        using var ms = new MemoryStream(buffer);
        transFile.ReadFrom(ms);

        return transFile;
    }

    private void SendTransportFile(string topic, TransportFile file, string replyTo)
    {
        _log.LogDebug($"Send file {file.Name} to {topic}, replyTo={replyTo}");
        _messaging.Publish(new Message
        {
            Topic = topic,
            Payload = JsonSerializer.Serialize(new TransportFileEnvelope
            {
                TransportFile = file.ToSerializableProperties(),
                ReplyTo = replyTo
            })
        });
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        _cancelTokenSource.Dispose();
        _messaging.Unsubscribe(_subscription);
    }
}