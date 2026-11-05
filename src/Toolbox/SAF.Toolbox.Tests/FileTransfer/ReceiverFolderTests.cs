// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Toolbox.FileTransfer;
using Testably.Abstractions.Testing;
using Xunit;

namespace SAF.Toolbox.Tests.FileTransfer;

public class ReceiverFolderTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly ReceiverFolder _folder;

    public ReceiverFolderTests()
    {
        _folder = new ReceiverFolder(_fileSystem, "folder");
    }

    [Theory]
    [InlineData("file.txt")]
    [InlineData("file")]
    [InlineData(".hidden")]
    [InlineData("..file")]
    [InlineData("file..txt")]
    [InlineData("AbC+12_xY")] // the alphabet of a file id
    public void IsSingleSegment_AcceptsAPlainName(string name)
        => Assert.True(_folder.IsSingleSegment(name));

    public static TheoryData<string> NamesWithAPath =>
    [
        "",
        ".",
        "..",
        "sub/file.txt",
        "file\0.txt",
        Path.Combine("..", "file.txt"),
        Path.Combine("sub", "file.txt"),
        Path.GetFullPath("file.txt")
    ];

    [Theory]
    [MemberData(nameof(NamesWithAPath))]
    public void IsSingleSegment_RefusesANameWithAPath(string name)
        => Assert.False(_folder.IsSingleSegment(name));

    [Fact]
    public void IsSingleSegment_RefusesNull()
        => Assert.False(_folder.IsSingleSegment(null));

    [Theory]
    [InlineData("file.txt")]
    [InlineData("..file.txt")]
    [InlineData("sub", "file.txt")]
    [InlineData("sub", "..", "file.txt")]
    public void Contains_AcceptsAPathInsideTheFolder(params string[] relativePath)
        => Assert.True(_folder.Contains(_fileSystem.Path.Combine([_folder.FullPath, ..relativePath])));

    [Theory]
    [InlineData]
    [InlineData("..")]
    [InlineData("..", "file.txt")]
    [InlineData("sub", "..", "..", "file.txt")]
    public void Contains_RefusesTheFolderItselfAndPathsAboveIt(params string[] relativePath)
        => Assert.False(_folder.Contains(_fileSystem.Path.Combine([_folder.FullPath, ..relativePath])));

    [Fact]
    public void Contains_RefusesASiblingFolderWhoseNameStartsLikeTheFolder()
        => Assert.False(_folder.Contains(_fileSystem.Path.Combine(_folder.FullPath + "2", "file.txt")));

    [Fact]
    public void Contains_RefusesAPathOnAnotherDrive()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows paths have drives.");
        var otherDrive = _folder.FullPath.StartsWith('Z') ? @"Y:\" : @"Z:\";

        Assert.False(_folder.Contains(otherDrive + "file.txt"));
    }

    [Fact]
    public void Contains_AcceptsAPathInsideAFolderGivenWithATrailingSeparator()
    {
        var folder = new ReceiverFolder(_fileSystem, "folder" + _fileSystem.Path.DirectorySeparatorChar);

        Assert.True(folder.Contains(_fileSystem.Path.Combine(_folder.FullPath, "file.txt")));
    }
}
