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
