// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using System.IO.Abstractions;

namespace SAF.Toolbox.FileTransfer;

/// <summary>
/// The folder a stateful receiver writes into. The sender names the file, so the receiver accepts only names and
/// target paths that keep the file inside this folder.
/// </summary>
internal sealed class ReceiverFolder(IFileSystem fileSystem, string folderPath)
{
    public string FullPath { get; } = fileSystem.Path.GetFullPath(folderPath);

    /// <summary>
    /// Whether <paramref name="name"/> is a single path segment: no directory, drive or stream separator, and
    /// neither <c>.</c> nor <c>..</c>. The file name and the file id become parts of file names in this folder.
    /// </summary>
    public bool IsSingleSegment(string? name)
        => !string.IsNullOrEmpty(name)
           && name is not ("." or "..")
           && name.IndexOfAny(fileSystem.Path.GetInvalidFileNameChars()) < 0;

    /// <summary>
    /// Whether <paramref name="fullFilePath"/> lies inside this folder or one of its subfolders. The folder itself
    /// does not count. Paths are compared as .NET compares them on the platform: ignoring case on Windows and macOS,
    /// but not on Linux.
    /// </summary>
    public bool Contains(string fullFilePath)
    {
        var relativePath = fileSystem.Path.GetRelativePath(FullPath, fullFilePath);
        return relativePath != "."
               && relativePath != ".."
               && !relativePath.StartsWith(".." + fileSystem.Path.DirectorySeparatorChar, StringComparison.Ordinal)
               && !fileSystem.Path.IsPathRooted(relativePath);
    }
}
