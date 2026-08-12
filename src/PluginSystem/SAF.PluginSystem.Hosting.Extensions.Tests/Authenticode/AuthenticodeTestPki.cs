// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests.Authenticode;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

/// <summary>
/// A throwaway certificate hierarchy: root CA, intermediate CA, and end-entity certificates issued by
/// the intermediate.
/// </summary>
/// <remarks>
/// Used together with <see cref="X509ChainTrustMode.CustomRootTrust"/>, which is the only way to build a
/// genuinely trusted chain on Windows, Linux and macOS alike without provisioning the machine's stores.
/// The hierarchy is deliberately two levels deep: the intermediate is never published anywhere, so a
/// chain only completes when it is carried inside the signature and picked up from the policy extra
/// store - exactly the situation an air-gapped host is in.
/// The CAs span a wide validity window so that a test can issue an end-entity certificate that is
/// already expired without the chain failing on the issuer instead.
/// </remarks>
internal sealed class AuthenticodeTestPki : IDisposable
{
    // 1.3.6.1.5.5.7.3.3 - id-kp-codeSigning, 1.3.6.1.5.5.7.3.8 - id-kp-timeStamping.
    internal const string CodeSigningOid = "1.3.6.1.5.5.7.3.3";
    internal const string TimeStampingOid = "1.3.6.1.5.5.7.3.8";

    private readonly List<X509Certificate2> _issued = [];

    public AuthenticodeTestPki()
    {
        Root = CreateSelfSignedCertificateAuthority("CN=SAF Authenticode test root");
        Intermediate = IssueCertificateAuthority("CN=SAF Authenticode test intermediate", Root);
    }

    public X509Certificate2 Root { get; }

    public X509Certificate2 Intermediate { get; }

    /// <summary>The roots to hand to the verifier as its trust anchors.</summary>
    public X509Certificate2Collection TrustAnchors => [Root];

    /// <summary>
    /// Issues an end-entity certificate from the intermediate. Callers choose validity and usage so a
    /// test can produce an expired signer, or one that is not allowed to sign code.
    /// </summary>
    public X509Certificate2 IssueEndEntityCertificate(
        string subject,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null,
        string enhancedKeyUsageOid = CodeSigningOid,
        bool criticalEnhancedKeyUsage = false)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(enhancedKeyUsageOid)], criticalEnhancedKeyUsage));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

        var certificate = Issue(
            request,
            Intermediate,
            notBefore ?? DateTimeOffset.UtcNow.AddHours(-1),
            notAfter ?? DateTimeOffset.UtcNow.AddHours(1),
            key);

        _issued.Add(certificate);
        return certificate;
    }

    /// <summary>Issues a certificate a time-stamping authority can sign RFC 3161 tokens with.</summary>
    /// <remarks>
    /// <see cref="System.Security.Cryptography.Pkcs.Rfc3161TimestampToken.TryDecode"/> holds the authority
    /// to the RFC and silently ignores a token that breaks it: the time-stamping usage must be the only
    /// one and marked critical, and this certificate must itself be valid at the instant the token claims,
    /// which is why its validity is wide.
    /// </remarks>
    public X509Certificate2 IssueTimestampAuthorityCertificate(
        string subject = "CN=SAF Authenticode test timestamp authority")
        => IssueEndEntityCertificate(
            subject,
            notBefore: DateTimeOffset.UtcNow.AddDays(-20),
            notAfter: DateTimeOffset.UtcNow.AddDays(20),
            enhancedKeyUsageOid: TimeStampingOid,
            criticalEnhancedKeyUsage: true);

    public void Dispose()
    {
        foreach (var certificate in _issued)
        {
            certificate.Dispose();
        }

        Intermediate.Dispose();
        Root.Dispose();
    }

    private static X509Certificate2 CreateSelfSignedCertificateAuthority(string subject)
    {
        using var key = RSA.Create(2048);
        return CreateCertificateAuthorityRequest(subject, key)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(30));
    }

    private static X509Certificate2 IssueCertificateAuthority(string subject, X509Certificate2 issuer)
    {
        using var key = RSA.Create(2048);
        return Issue(
            CreateCertificateAuthorityRequest(subject, key),
            issuer,
            DateTimeOffset.UtcNow.AddDays(-29),
            DateTimeOffset.UtcNow.AddDays(29),
            key);
    }

    private static CertificateRequest CreateCertificateAuthorityRequest(string subject, RSA key)
    {
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));
        return request;
    }

    private static X509Certificate2 Issue(
        CertificateRequest request,
        X509Certificate2 issuer,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter,
        RSA subjectKey)
    {
        var serialNumber = new byte[16];
        RandomNumberGenerator.Fill(serialNumber);

        using var certificate = request.Create(issuer, notBefore, notAfter, serialNumber);
        return certificate.CopyWithPrivateKey(subjectKey);
    }
}
