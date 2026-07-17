// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Authenticode;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Builds a certificate chain for the signing certificate on platforms that do not offer a native
/// Authenticode API (Linux, macOS). Trust is anchored in the platform certificate store, so the
/// result depends on which roots are provisioned on the host.
/// <para>
/// The file-content binding is verified separately by <see cref="AuthenticodePeHasher"/>; this type
/// only decides whether the signer chains to a trusted root and carries the code-signing usage.
/// </para>
/// </summary>
internal sealed class CrossPlatformAuthenticodeTrustVerifier : IAuthenticodeChainTrustVerifier
{
    // 1.3.6.1.5.5.7.3.3 - id-kp-codeSigning
    private const string CodeSigningEnhancedKeyUsageOid = "1.3.6.1.5.5.7.3.3";

    // This verifier only validates the certificate chain; the file binding must be checked
    // separately, so callers must not treat a trusted result as proof the signature covers the file.
    public bool VerifiesFileIntegrity => false;

    public bool IsTrusted(string assemblyPath, X509Certificate2 signerCertificate)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid(CodeSigningEnhancedKeyUsageOid));

        return chain.Build(signerCertificate);
    }
}
