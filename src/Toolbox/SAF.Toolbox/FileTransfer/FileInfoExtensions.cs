// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.IO.Abstractions;

namespace SAF.Toolbox.FileTransfer;

using System.Security.Cryptography;

internal static class FileInfoExtensions
{
    public static string GetFileId(this IFileInfo fileInfo, uint chunkSize)
    {
        ArgumentNullException.ThrowIfNull(fileInfo);
        if (!fileInfo.Exists) throw new FileNotFoundException("File does not exist", fileInfo.FullName);

        var meta = $"{fileInfo.Name}|{fileInfo.Length}|{chunkSize}|{Convert.ToHexString(HashContent(fileInfo))}";
        var metaHash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(meta));

        return Convert.ToBase64String(metaHash);
    }

    public static string GetContentHash(this IFileInfo fileInfo)
        => Convert.ToBase64String(fileInfo.HashContent());

    private static byte[] HashContent(this IFileInfo fileInfo)
    {
        ArgumentNullException.ThrowIfNull(fileInfo);
        if (!fileInfo.Exists) throw new FileNotFoundException("File does not exist", fileInfo.FullName);

        using var fs = fileInfo.OpenRead();
        return SHA256.HashData(fs);
    }
}