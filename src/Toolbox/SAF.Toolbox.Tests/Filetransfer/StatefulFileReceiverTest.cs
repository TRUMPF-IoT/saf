// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.Tests.Filetransfer;

using Toolbox.Filetransfer;
using Xunit;
using Xunit.Abstractions;

public class StatefulFileReceiverTest
{
    private readonly ITestOutputHelper _testOutputHelper;

    public string DirectoryName { get; }

    public StatefulFileReceiverTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        DirectoryName = Path.Combine(Directory.GetCurrentDirectory(), "fileReceiverTest");
        if (Directory.Exists(DirectoryName)) Directory.Delete(DirectoryName, true);
    }

    [Theory]
    [InlineData(1, null)] // 1 byte
    [InlineData(1024, null)] // 1 kByte
    [InlineData(1024 * 3, null)]  // 3 kByte
    [InlineData(1024 * 1024, null)] // 1 MByte
    [InlineData(1024 * 1024 * 3, null)]  // 3 MByte
    [InlineData(FileSender.MaxChunkSize - 1, null)] // excact chunk size - 1
    [InlineData(FileSender.MaxChunkSize, null)] // excact chunk size
    [InlineData(FileSender.MaxChunkSize + 1, null)] // excact chunk size + 1
    [InlineData(FileSender.MaxChunkSize * 3 - 1, null)] // multiple of chunk size - 1
    [InlineData(FileSender.MaxChunkSize * 3, null)] // multiple of chunk size
    [InlineData(FileSender.MaxChunkSize * 3 + 1, null)] // multiple of chunk size + 1
    [InlineData(FileSender.MaxChunkSize * 3 - 1, 1234L)] // multiple of chunk size - 1, with transfer id
    [InlineData(1024 * 1024 * 3, 5678L)]  // 3 MByte, with transfer id
    public async Task TestReceiveChunksOk(int fileSize, long? transferId)
    {
        var fileName = $"test-{fileSize}.file";
        var mimeType = "application/octet-stream";
        var channelName = "statefulReceiverTest";
        var directoryName = DirectoryName;
        var targetFile = Path.Combine(directoryName, fileName);

        var uniqueTransferId = transferId ?? 0;
        var tempFileName = Path.ChangeExtension(targetFile, $".{uniqueTransferId}.temp");

        var maxChunkSize = FileSender.MaxChunkSize;

        var srcData = new byte[fileSize];
        var rand = new Random();
        rand.NextBytes(srcData);

        var chunkSize = srcData.Length < maxChunkSize ? srcData.Length : maxChunkSize;
        var length = srcData.LongLength;
        var offset = 0L;
        var n = 0;

        var receiver = new StatefulFileReceiver(new TestLogger<StatefulFileReceiver>(_testOutputHelper));
        string? receivedFile = null;
        receiver.FileReceived += file => { receivedFile = file; };

        var parallelActions = new List<Task>();
        for (; offset < length - chunkSize; offset += chunkSize, n += 1)
        {
            var callChunk = n;
            var callOffset = offset;
            _testOutputHelper.WriteLine($"Prepare chunk {callChunk} action for parallel processing");

            parallelActions.Add(Task.Run(() =>
            {
                var transFile = CreateTransportFile(fileName, mimeType, callChunk, srcData, (int)callOffset, (int)chunkSize, false, transferId);
                receiver.WriteFile(directoryName, new TransportFileDelivery
                {
                    Channel = channelName,
                    IsConsistent = true,
                    Timestamp = DateTimeOffset.UtcNow,
                    TransportFile = transFile
                }, true);
            }));
        }

        parallelActions.Add(Task.Run(() =>
        {
            _testOutputHelper.WriteLine($"Prepare last chunk {n} action for parallel processing");

            var transFile = CreateTransportFile(fileName, mimeType, n, srcData, (int)offset, (int)(length - offset), true, transferId);
            receiver.WriteFile(directoryName, new TransportFileDelivery
            {
                Channel = channelName,
                IsConsistent = true,
                Timestamp = DateTimeOffset.UtcNow,
                TransportFile = transFile
            }, true);
        }));

        await Task.WhenAll(parallelActions);

        Assert.True(File.Exists(targetFile));
        Assert.Equal(targetFile, receivedFile);

        var targetFileBytes = await File.ReadAllBytesAsync(targetFile);
        Assert.Equal(fileSize, targetFileBytes.Length);
        Assert.True(srcData.SequenceEqual(targetFileBytes));

        Assert.False(File.Exists(tempFileName));
    }

    private TransportFile CreateTransportFile(string fileName, string mimeType, int chunkNumber, byte[] srcData, int offset, int chunkSize, bool lastChunk, long? transferId)
    {
        TransportFile transFile;
        using (var ms = new MemoryStream(srcData, offset, chunkSize))
        {
            transFile = new TransportFile(fileName, mimeType,
                CreateTransFileProperties(lastChunk, chunkNumber, srcData.Length, chunkSize, offset, transferId));
            transFile.ReadFrom(ms);
        }

        using (var ms = new MemoryStream(srcData, offset, chunkSize))
        {
            var propsWithFingerPrint = transFile.ToSerializableProperties();
            transFile = new TransportFile(fileName, mimeType, propsWithFingerPrint);
            transFile.ReadFrom(ms);
        }

        return transFile;
    }

    private IDictionary<string, string> CreateTransFileProperties(bool isLast, int chunkNumber, long fileSize, long chunkSize, long chunkOffset, long? transferId)
    {
        var properties = new Dictionary<string, string>
        {
            [FileTransferIdentifiers.Chunked] = $"{true}",
            [FileTransferIdentifiers.LastChunk] = $"{isLast}",
            [FileTransferIdentifiers.ChunkNumber] = $"{chunkNumber}",
            [FileTransferIdentifiers.FileSize] = $"{fileSize}",
            [FileTransferIdentifiers.ChunkSize] = $"{chunkSize}",
            [FileTransferIdentifiers.ChunkOffset] = $"{chunkOffset}"
        };

        if(transferId != null)
            properties.Add(FileTransferIdentifiers.TransferId, $"{transferId}");

        return properties;
    }
}