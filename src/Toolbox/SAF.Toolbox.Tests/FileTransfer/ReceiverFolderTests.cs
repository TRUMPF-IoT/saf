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
    public void PathInside_ReturnsTheFullPath_ForAPathInsideTheFolder(params string[] relativePath)
    {
        var filePath = _fileSystem.Path.Combine([_folder.FullPath, ..relativePath]);

        Assert.Equal(_fileSystem.Path.GetFullPath(filePath), _folder.PathInside(filePath));
    }

    [Theory]
    [InlineData]
    [InlineData("..")]
    [InlineData("..", "file.txt")]
    [InlineData("sub", "..", "..", "file.txt")]
    public void PathInside_RefusesTheFolderItselfAndPathsAboveIt(params string[] relativePath)
        => Assert.Null(_folder.PathInside(_fileSystem.Path.Combine([_folder.FullPath, ..relativePath])));

    [Fact]
    public void PathInside_RefusesTheFolderGivenWithATrailingSeparator()
        => Assert.Null(_folder.PathInside(_folder.FullPath + _fileSystem.Path.DirectorySeparatorChar));

    [Fact]
    public void PathInside_RefusesASiblingFolderWhoseNameStartsLikeTheFolder()
        => Assert.Null(_folder.PathInside(_fileSystem.Path.Combine(_folder.FullPath + "2", "file.txt")));

    // On a file system that distinguishes case, the path names a sibling folder. It must not lead there.
    [Fact]
    public void PathInside_SpellsTheFolderAsItself_WhenThePathSpellsItInAnotherCase()
    {
        var filePath = _fileSystem.Path.Combine(_folder.FullPath.ToUpperInvariant(), "Sub", "File.txt");

        Assert.Equal(_fileSystem.Path.Combine(_folder.FullPath, "Sub", "File.txt"), _folder.PathInside(filePath));
    }

    [Fact]
    public void PathInside_RefusesAPathOnAnotherDrive()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows paths have drives.");
        var otherDrive = _folder.FullPath.StartsWith('Z') ? @"Y:\" : @"Z:\";

        Assert.Null(_folder.PathInside(otherDrive + "file.txt"));
    }

    [Fact]
    public void PathInside_AcceptsAPathInsideAFolderGivenWithATrailingSeparator()
    {
        var folder = new ReceiverFolder(_fileSystem, "folder" + _fileSystem.Path.DirectorySeparatorChar);
        var filePath = _fileSystem.Path.Combine(_folder.FullPath, "file.txt");

        Assert.Equal(filePath, folder.PathInside(filePath));
    }

    [Fact]
    public void PathInside_AcceptsAPathInsideARootFolder()
    {
        var root = _fileSystem.Path.GetPathRoot(_folder.FullPath)!;
        var folder = new ReceiverFolder(_fileSystem, root);
        var filePath = _fileSystem.Path.Combine(root, "file.txt");

        Assert.Equal(filePath, folder.PathInside(filePath));
        Assert.Null(folder.PathInside(root));
    }
}
