// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.FileTransfer;

internal static class TransportFileExtensions
{
    public static string GetTargetFilePath(this TransportFile file, string folderPath)
        => Path.Combine(folderPath, file.FileName);

    public static string GetTempTargetFilePath(this TransportFile file, string folderPath)
    {
        var targetFilePath = file.GetTargetFilePath(folderPath);
        return Path.ChangeExtension(targetFilePath, $".{file.FileId}.temp");
    }

    public static string GetMetadataTargetFilePath(this TransportFile file, string folderPath)
    {
        var targetFilePath = file.GetTargetFilePath(folderPath);
        return Path.ChangeExtension(targetFilePath, $".{file.FileId}.meta");
    }
}