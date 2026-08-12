// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Authenticode;

using System.Security.Cryptography.Pkcs;

/// <summary>
/// Decides whether the signer of an Authenticode signature is trusted on the current platform.
/// </summary>
internal interface IAuthenticodeChainTrustVerifier
{
    /// <summary>
    /// Gets a value indicating whether a trusted result also proves that the signature covers the file
    /// content. When false, the caller must verify the embedded PE hash separately.
    /// </summary>
    bool VerifiesFileIntegrity { get; }

    /// <summary>
    /// Gets a value indicating whether the verifier inspects the file itself. When false, callers that
    /// only hold a content snapshot do not need to materialize it on disk.
    /// </summary>
    bool RequiresFilePath { get; }

    /// <summary>
    /// Determines whether the signer of <paramref name="signedCms"/> is trusted.
    /// </summary>
    /// <param name="assemblyPath">The path of the signed file, or null when the verifier does not need one.</param>
    /// <param name="signedCms">The decoded Authenticode signature, carrying the signer info and the
    /// certificates embedded in the PKCS#7 blob.</param>
    bool IsTrusted(string? assemblyPath, SignedCms signedCms);
}
