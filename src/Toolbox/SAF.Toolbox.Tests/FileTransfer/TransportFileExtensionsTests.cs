// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Toolbox.FileTransfer;
using Xunit;

namespace SAF.Toolbox.Tests.FileTransfer;

public class TransportFileExtensionsTests
{
    private readonly TransportFile _transportFile = new TransportFile
    {
        FileName = "myfile.txt",
        FileId = "abc123",
        ContentHash = "hash123",
        ContentLength = 1024,
        ChunkSize = 256,
        TotalChunks = 4
    };

    private static readonly string Folder = AppDomain.CurrentDomain.BaseDirectory;

    [Fact]
    public void GetTargetFilePath_ReturnsCombinedPath()
    {
        var result = _transportFile.GetTargetFilePath(Folder);
        Assert.Equal(Path.Combine(Folder, "myfile.txt"), result);
    }

    [Fact]
    public void GetTempTargetFilePath_ReturnsTempFilePathWithFileId()
    {
        var result = _transportFile.GetTempTargetFilePath(Folder);
        Assert.Equal(Path.Combine(Folder, "myfile.abc123.temp"), result);
    }

    [Fact]
    public void GetMetadataTargetFilePath_ReturnsMetaFilePathWithFileId()
    {
        var result = _transportFile.GetMetadataTargetFilePath(Folder);
        Assert.Equal(Path.Combine(Folder, "myfile.abc123.meta"), result);
    }
}