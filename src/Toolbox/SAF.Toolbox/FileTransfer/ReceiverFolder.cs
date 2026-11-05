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
    /// The full path inside this folder or one of its subfolders that <paramref name="filePath"/> names, or
    /// <c>null</c> for the folder itself and for paths outside it. The folder part is compared ignoring case and
    /// returned in this folder's spelling, the rest as given. A path that spells the folder in another case therefore
    /// stays inside this folder, also on a file system that distinguishes case and would take it for another folder.
    /// </summary>
    public string? PathInside(string filePath)
    {
        // A full path holds no . or .. segments, so whatever follows the folder part stays below the folder.
        var fullFilePath = fileSystem.Path.GetFullPath(filePath);
        var folderPrefix = fileSystem.Path.EndsInDirectorySeparator(FullPath)
            ? FullPath
            : FullPath + fileSystem.Path.DirectorySeparatorChar;

        if (fullFilePath.Length <= folderPrefix.Length
            || !fullFilePath.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return folderPrefix + fullFilePath[folderPrefix.Length..];
    }
}
