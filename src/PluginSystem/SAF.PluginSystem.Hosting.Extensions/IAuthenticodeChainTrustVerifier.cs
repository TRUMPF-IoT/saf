// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions;

using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Determines whether a code-signing certificate (and, where the platform supports it, the whole
/// signed file) is trusted according to the platform's Authenticode trust policy.
/// </summary>
internal interface IAuthenticodeChainTrustVerifier
{
    /// <summary>
    /// <see langword="true"/> when a positive <see cref="IsTrusted"/> result also guarantees that the
    /// signature covers the file contents (as the Windows <c>WinVerifyTrust</c> path does), so the
    /// caller can skip an additional PE-hash comparison. <see langword="false"/> when this verifier
    /// only validates the certificate chain and the file binding must be checked separately.
    /// </summary>
    bool VerifiesFileIntegrity { get; }

    bool IsTrusted(string assemblyPath, X509Certificate2 signerCertificate);
}
