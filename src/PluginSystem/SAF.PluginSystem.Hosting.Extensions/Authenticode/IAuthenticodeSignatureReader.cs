// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Authenticode;

internal interface IAuthenticodeSignatureReader
{
    /// <summary>
    /// Reads and verifies the Authenticode signature embedded in the given file.
    /// </summary>
    /// <param name="assemblyPath">Path to the PE file to inspect.</param>
    /// <returns>
    /// <see langword="null"/> when the file carries no Authenticode signature at all;
    /// otherwise the signature information. A signer thumbprint is only reported when the
    /// signature is cryptographically intact and actually covers the file contents.
    /// </returns>
    AuthenticodeSignatureInfo? ReadSignature(string assemblyPath);

    /// <summary>
    /// Reads and verifies the Authenticode signature embedded in an assembly content snapshot. The check
    /// runs entirely in memory and never touches the file system.
    /// </summary>
    /// <param name="assemblyBytes">The stable PE file content to inspect.</param>
    /// <returns>
    /// <see langword="null"/> when the content carries no Authenticode signature at all;
    /// otherwise the signature information. Trust is anchored by certificate chain building instead of by
    /// the platform Authenticode API, which would need a file. Both use the same chain engine and the same
    /// certificate stores; what this route does not apply is the Authenticode policy layer above the
    /// chain, most notably the weak-hash policy.
    /// </returns>
    AuthenticodeSignatureInfo? ReadSignature(ReadOnlyMemory<byte> assemblyBytes);
}

/// <summary>
/// Describes the result of reading a PE file's Authenticode signature.
/// </summary>
/// <param name="SignerThumbprint">
/// Thumbprint of the signing certificate, or <see langword="null"/> when the signature does not
/// verifiably cover the file (e.g. a tampered file or a transplanted signature blob).
/// </param>
/// <param name="HasValidDigitalSignature">
/// <see langword="true"/> when the signature covers the file, chains to a trusted root and passes
/// the platform's Authenticode trust policy.
/// </param>
internal sealed record AuthenticodeSignatureInfo(string? SignerThumbprint, bool HasValidDigitalSignature);
