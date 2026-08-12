// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Authenticode;

internal interface IAuthenticodeCertificateTableParser
{
    bool IsWellFormed(Stream stream, long offset, long size);

    bool TryExtractPkcsSignedData(ReadOnlySpan<byte> certificateBlob, out byte[]? signedData);
}