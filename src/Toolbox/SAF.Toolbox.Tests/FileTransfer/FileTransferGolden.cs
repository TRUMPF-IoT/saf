// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.Tests.FileTransfer;

using SAF.Toolbox.FileTransfer;
using Xunit;

/// <summary>
/// File transfer payloads as written by the code. The DTOs and the serializer are unchanged since SAF 9.x,
/// so the payloads without chunk formats and binary chunks are what SAF 9.x and 10.x read and write.
/// </summary>
internal static class FileTransferGolden
{
    public static TransportFile File => new()
    {
        FileName = "file.bin",
        FileId = "fileId",
        ContentHash = "hash",
        ContentLength = 10,
        ChunkSize = 4,
        TotalChunks = 3,
        Properties = new Dictionary<string, string> { ["key"] = "value" }
    };

    public const string StateRequest =
        """{"file":{"fileName":"file.bin","fileId":"fileId","contentHash":"hash","contentLength":10,"chunkSize":4,"totalChunks":3,"properties":{"key":"value"}},"replyTo":"reply/1"}""";

    public const string Saf10StateResponse = """{"state":{"fileExists":false,"transmittedChunks":[0,2]}}""";

    public const string StateResponse = """{"state":{"fileExists":false,"transmittedChunks":[0,2]},"acceptedChunkFormats":3}""";

    public const string TextChunkRequest =
        """{"file":{"fileName":"file.bin","fileId":"fileId","contentHash":"hash","contentLength":10,"chunkSize":4,"totalChunks":3,"properties":{"key":"value"}},"fileChunk":{"index":1,"data":"BAUGBw=="},"replyTo":"reply/1"}""";

    public const string BinaryChunkRequest =
        """{"file":{"fileName":"file.bin","fileId":"fileId","contentHash":"hash","contentLength":10,"chunkSize":4,"totalChunks":3,"properties":{"key":"value"}},"index":1}""";

    public const string ChunkResponse = """{"status":0}""";

    public static byte[] ChunkData => [4, 5, 6, 7];

    public static void AssertIsFile(TransportFile file)
    {
        var expected = File;
        Assert.Equal(expected.FileName, file.FileName);
        Assert.Equal(expected.FileId, file.FileId);
        Assert.Equal(expected.ContentHash, file.ContentHash);
        Assert.Equal(expected.ContentLength, file.ContentLength);
        Assert.Equal(expected.ChunkSize, file.ChunkSize);
        Assert.Equal(expected.TotalChunks, file.TotalChunks);
        Assert.Equal(expected.Properties, file.Properties);
    }
}
