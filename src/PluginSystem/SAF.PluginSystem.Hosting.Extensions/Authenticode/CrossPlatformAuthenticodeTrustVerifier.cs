// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Authenticode;

using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
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

    // 1.2.840.113549.1.9.16.2.14 - id-aa-timeStampToken (RFC 3161)
    private const string Rfc3161TimestampTokenOid = "1.2.840.113549.1.9.16.2.14";

    // 1.2.840.113549.1.9.5 - PKCS#9 signingTime, used by the legacy Authenticode counter-signature
    private const string SigningTimeOid = "1.2.840.113549.1.9.5";

    private readonly X509Certificate2Collection? _customTrustAnchors;

    public CrossPlatformAuthenticodeTrustVerifier()
    {
    }

    /// <summary>
    /// Anchors trust in an explicit set of roots instead of the platform certificate stores.
    /// </summary>
    /// <remarks>
    /// Lets a caller decide the trust anchors itself, which is the only way to obtain a trusted chain
    /// without provisioning the host store - the case for tests, and for hosts whose store cannot be
    /// changed. Only the anchors are replaced; every other rule of the policy still applies.
    /// </remarks>
    internal CrossPlatformAuthenticodeTrustVerifier(X509Certificate2Collection customTrustAnchors)
    {
        ArgumentNullException.ThrowIfNull(customTrustAnchors);
        _customTrustAnchors = customTrustAnchors;
    }

    // This verifier only validates the certificate chain; the file binding must be checked
    // separately, so callers must not treat a trusted result as proof the signature covers the file.
    public bool VerifiesFileIntegrity => false;

    // Everything needed is in the decoded signature, so the file itself is never opened.
    public bool RequiresFilePath => false;

    public bool IsTrusted(string? assemblyPath, SignedCms signedCms)
    {
        ArgumentNullException.ThrowIfNull(signedCms);

        if (signedCms.SignerInfos.Count == 0)
        {
            return false;
        }

        var signerInfo = signedCms.SignerInfos[0];
        var signerCertificate = signerInfo.Certificate;
        if (signerCertificate is null)
        {
            return false;
        }

        using var chain = new X509Chain { ChainPolicy = CreateChainPolicy(signedCms, signerInfo) };
        return chain.Build(signerCertificate);
    }

    internal X509ChainPolicy CreateChainPolicy(SignedCms signedCms, SignerInfo signerInfo)
    {
        var policy = new X509ChainPolicy
        {
            RevocationMode = X509RevocationMode.NoCheck,
            RevocationFlag = X509RevocationFlag.ExcludeRoot,
            VerificationFlags = X509VerificationFlags.NoFlag
        };
        policy.ApplicationPolicy.Add(new Oid(CodeSigningEnhancedKeyUsageOid));

        if (_customTrustAnchors is not null)
        {
            policy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            policy.CustomTrustStore.AddRange(_customTrustAnchors);
        }

        // Authenticode carries the leaf and its intermediates inside the PKCS#7 blob, while platform
        // certificate stores hold roots only. Without them the chain ends in PartialChain on every host
        // that cannot fetch the issuer via AIA, which is the normal case for air-gapped machines.
        policy.ExtraStore.AddRange(signedCms.Certificates);

        // Match the Windows verifier: validate against the time the signature was counter-signed when it
        // is timestamped, and against the current time otherwise.
        var signatureTimestamp = TryGetSignatureTimestamp(signerInfo);
        if (signatureTimestamp is not null)
        {
            policy.VerificationTime = signatureTimestamp.Value.UtcDateTime;
        }

        return policy;
    }

    private static DateTimeOffset? TryGetSignatureTimestamp(SignerInfo signerInfo)
        => TryGetRfc3161Timestamp(signerInfo) ?? TryGetCounterSignerSigningTime(signerInfo);

    private static DateTimeOffset? TryGetRfc3161Timestamp(SignerInfo signerInfo)
    {
        foreach (var attribute in signerInfo.UnsignedAttributes)
        {
            if (attribute.Oid?.Value != Rfc3161TimestampTokenOid)
            {
                continue;
            }

            foreach (var value in attribute.Values)
            {
                if (Rfc3161TimestampToken.TryDecode(value.RawData, out var token, out _))
                {
                    return token.TokenInfo.Timestamp;
                }
            }
        }

        return null;
    }

    private static DateTimeOffset? TryGetCounterSignerSigningTime(SignerInfo signerInfo)
    {
        foreach (var counterSignerInfo in signerInfo.CounterSignerInfos)
        {
            foreach (var attribute in counterSignerInfo.SignedAttributes)
            {
                if (attribute.Oid?.Value != SigningTimeOid)
                {
                    continue;
                }

                foreach (var value in attribute.Values)
                {
                    if (value is Pkcs9SigningTime signingTime)
                    {
                        return signingTime.SigningTime;
                    }

                    try
                    {
                        return new Pkcs9SigningTime(value.RawData).SigningTime;
                    }
                    catch (CryptographicException)
                    {
                        // Malformed attribute: fall through to the next candidate.
                    }
                }
            }
        }

        return null;
    }
}
