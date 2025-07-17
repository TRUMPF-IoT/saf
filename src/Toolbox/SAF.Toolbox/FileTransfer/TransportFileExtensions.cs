// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.IO.Abstractions;

namespace SAF.Toolbox.FileTransfer;

internal static class TransportFileExtensions
{
    public static string GetTargetFilePath(this TransportFile file, IFileSystem fileSystem, string folderPath)
        => fileSystem.Path.GetFullPath(fileSystem.Path.Combine(folderPath, file.FileName));

    public static string GetTempTargetFilePath(this TransportFile file, IFileSystem fileSystem, string folderPath)
    {
        var targetFilePath = file.GetTargetFilePath(fileSystem, folderPath);
        return fileSystem.Path.ChangeExtension(targetFilePath, $".{file.FileId}.temp");
    }

    public static string GetMetadataTargetFilePath(this TransportFile file, IFileSystem fileSystem, string folderPath)
    {
        var targetFilePath = file.GetTargetFilePath(fileSystem, folderPath);
        return fileSystem.Path.ChangeExtension(targetFilePath, $".{file.FileId}.meta");
    }
}