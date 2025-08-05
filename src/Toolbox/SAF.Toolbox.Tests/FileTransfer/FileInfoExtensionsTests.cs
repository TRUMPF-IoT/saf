// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.IO.Abstractions.TestingHelpers;
using SAF.Toolbox.FileTransfer;

namespace SAF.Toolbox.Tests.FileTransfer;

using System.IO.Abstractions;
using Xunit;

public class FileInfoExtensionsTests
{
    private readonly MockFileSystem _mockFileSystem = new();

    [Fact]
    public void GetFileId_ThrowsArgumentNullException_WhenFileInfoIsNull()
    {
        IFileInfo fileInfo = null!;
        Assert.Throws<ArgumentNullException>(() => fileInfo.GetFileId(1024));
    }

    [Fact]
    public void GetFileId_ThrowsFileNotFoundException_WhenFileDoesNotExist()
    {
        var fileInfo = _mockFileSystem.FileInfo.New("notfound.txt");

        var ex = Assert.Throws<FileNotFoundException>(() => fileInfo.GetFileId(1024));
        Assert.Contains("notfound.txt", ex.FileName);
    }

    [Theory]
    [InlineData(1024)] // 1 kByte
    [InlineData(4096)] // 4 kByte
    [InlineData(8192)] // 8 kByte
    [InlineData(16384)] // 16 kByte
    [InlineData(32768)] // 32 kByte
    public void GetFileId_ReturnsDeterministicId_ForSameFilePropertiesAndContent(uint contentLength)
    {
        var rnd = new Random();
        var fileContent = new byte[contentLength];
        rnd.NextBytes(fileContent);

        _mockFileSystem.AddFile("file.txt", new MockFileData(fileContent));
        var fileInfo = _mockFileSystem.FileInfo.New("file.txt");

        var id1 = fileInfo.GetFileId(4096);
        var id2 = fileInfo.GetFileId(4096);

        Assert.Equal(id1, id2);
    }

    [Fact]
    public void GetContentHash_ThrowsArgumentNullException_WhenFileInfoIsNull()
    {
        IFileInfo fileInfo = null!;
        Assert.Throws<ArgumentNullException>(fileInfo.GetContentHash);
    }

    [Fact]
    public void GetContentHash_ThrowsFileNotFoundException_WhenFileDoesNotExist()
    {
        var fileInfo = _mockFileSystem.FileInfo.New("notfound.txt");

        var ex = Assert.Throws<FileNotFoundException>(fileInfo.GetContentHash);
        Assert.Contains("notfound.txt", ex.FileName);
    }

    [Theory]
    [InlineData(1024)] // 1 kByte
    [InlineData(4096)] // 4 kByte
    [InlineData(8192)] // 8 kByte
    [InlineData(16384)] // 16 kByte
    [InlineData(32768)] // 32 kByte
    public void GetContentHash_ReturnsBase64HashOfContent(uint contentLength)
    {
        var rnd = new Random();
        var fileContent = new byte[contentLength];
        rnd.NextBytes(fileContent);

        _mockFileSystem.AddFile("file.txt", new MockFileData(fileContent));
        var fileInfo = _mockFileSystem.FileInfo.New("file.txt");

        var expectedHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(fileContent));
        var actualHash = fileInfo.GetContentHash();

        Assert.Equal(expectedHash, actualHash);
    }

    [Theory]
    [InlineData(1024)] // 1 kByte
    [InlineData(4096)] // 4 kByte
    [InlineData(8192)] // 8 kByte
    [InlineData(16384)] // 16 kByte
    [InlineData(32768)] // 32 kByte
    public void GetContentHash_ReturnsDeterministicHash_ForSameFileContent(uint contentLength)
    {
        var rnd = new Random();
        var fileContent = new byte[contentLength];
        rnd.NextBytes(fileContent);

        _mockFileSystem.AddFile("file.txt", new MockFileData(fileContent));
        var fileInfo = _mockFileSystem.FileInfo.New("file.txt");

        var id1 = fileInfo.GetContentHash();
        var id2 = fileInfo.GetContentHash();

        Assert.Equal(id1, id2);
    }
}