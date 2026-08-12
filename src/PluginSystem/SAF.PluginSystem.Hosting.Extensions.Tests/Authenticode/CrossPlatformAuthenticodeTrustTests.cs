// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests.Authenticode;

using SAF.PluginSystem.Hosting.Extensions.Authenticode;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Covers the positive side of chain building, which the machine trust stores cannot provide: no host
/// trusts a certificate this test suite could issue. Anchoring the policy in a throwaway root makes the
/// accepted cases assertable on every operating system.
/// </summary>
public sealed class CrossPlatformAuthenticodeTrustTests
{
    [Fact]
    public void IsTrusted_AcceptsSigner_ChainingToATrustedRoot()
    {
        using var pki = new AuthenticodeTestPki();
        using var signer = pki.IssueEndEntityCertificate("CN=SAF Authenticode trusted signer");
        var signedCms = CreateSignedCms(signer, pki.Intermediate);
        var verifier = new CrossPlatformAuthenticodeTrustVerifier(pki.TrustAnchors);

        Assert.True(verifier.IsTrusted(assemblyPath: null, signedCms));
    }

    [Fact]
    public void IsTrusted_RejectsSigner_WhenTheIntermediateIsNotEmbedded()
    {
        using var pki = new AuthenticodeTestPki();
        using var signer = pki.IssueEndEntityCertificate("CN=SAF Authenticode orphan signer");
        var signedCms = CreateSignedCms(signer);
        var verifier = new CrossPlatformAuthenticodeTrustVerifier(pki.TrustAnchors);

        // The intermediate is published nowhere, so the chain can only complete through the extra store.
        // This is the failure the extra store exists to prevent.
        Assert.False(verifier.IsTrusted(assemblyPath: null, signedCms));
    }

    [Fact]
    public void IsTrusted_RejectsSigner_WithoutTheCodeSigningUsage()
    {
        using var pki = new AuthenticodeTestPki();
        using var signer = pki.IssueEndEntityCertificate(
            "CN=SAF Authenticode timestamping signer",
            enhancedKeyUsageOid: AuthenticodeTestPki.TimeStampingOid);
        var signedCms = CreateSignedCms(signer, pki.Intermediate);
        var verifier = new CrossPlatformAuthenticodeTrustVerifier(pki.TrustAnchors);

        Assert.False(verifier.IsTrusted(assemblyPath: null, signedCms));
    }

    [Fact]
    public void IsTrusted_RejectsExpiredSigner_WhenTheSignatureIsNotTimestamped()
    {
        using var pki = new AuthenticodeTestPki();
        using var signer = ExpiredSigner(pki);
        var signedCms = CreateSignedCms(signer, pki.Intermediate);
        var verifier = new CrossPlatformAuthenticodeTrustVerifier(pki.TrustAnchors);

        Assert.False(verifier.IsTrusted(assemblyPath: null, signedCms));
    }

    [Fact]
    public void IsTrusted_AcceptsExpiredSigner_WhenTheCounterSignatureProvesItSignedWhileValid()
    {
        using var pki = new AuthenticodeTestPki();
        using var signer = ExpiredSigner(pki);
        var signedCms = CreateCounterSignedCms(signer, DateTimeOffset.UtcNow.AddDays(-5), embedded: pki.Intermediate);
        var verifier = new CrossPlatformAuthenticodeTrustVerifier(pki.TrustAnchors);

        // An intact signature must not rot when the signing certificate expires - which is what
        // WinVerifyTrust does, and what the same code must do everywhere else.
        Assert.True(verifier.IsTrusted(assemblyPath: null, signedCms));
    }

    [Fact]
    public void IsTrusted_AcceptsExpiredSigner_WhenAnRfc3161TokenProvesItSignedWhileValid()
    {
        using var pki = new AuthenticodeTestPki();
        using var signer = ExpiredSigner(pki);
        using var timestampAuthority = pki.IssueTimestampAuthorityCertificate();
        var signedCms = CreateTimestampedCms(signer, timestampAuthority, DateTimeOffset.UtcNow.AddDays(-5), embedded: pki.Intermediate);

        var verifier = new CrossPlatformAuthenticodeTrustVerifier(pki.TrustAnchors);

        // RFC 3161 is what a signtool signature actually carries; the counter-signature form is legacy.
        Assert.True(verifier.IsTrusted(assemblyPath: null, signedCms));
    }

    [Fact]
    public void IsTrusted_PrefersTheRfc3161Token_OverTheCounterSignature()
    {
        using var pki = new AuthenticodeTestPki();
        using var signer = ExpiredSigner(pki);
        using var timestampAuthority = pki.IssueTimestampAuthorityCertificate();

        var signedCms = CreateTimestampedCms(
            signer,
            timestampAuthority,
            DateTimeOffset.UtcNow.AddDays(-5),
            // The counter-signature claims a time at which the signer had already expired. Accepting the
            // signature means the RFC 3161 token decided, not this.
            counterSignatureTime: DateTimeOffset.UtcNow,
            embedded: pki.Intermediate);

        var verifier = new CrossPlatformAuthenticodeTrustVerifier(pki.TrustAnchors);

        Assert.True(verifier.IsTrusted(assemblyPath: null, signedCms));
    }

    [Fact]
    public void IsTrusted_RejectsExpiredSigner_WhenTheRfc3161TokenIsAlsoOutsideValidity()
    {
        using var pki = new AuthenticodeTestPki();
        using var signer = ExpiredSigner(pki);
        using var timestampAuthority = pki.IssueTimestampAuthorityCertificate();
        var signedCms = CreateTimestampedCms(signer, timestampAuthority, DateTimeOffset.UtcNow, embedded: pki.Intermediate);

        var verifier = new CrossPlatformAuthenticodeTrustVerifier(pki.TrustAnchors);

        // A timestamp must move the verification time, not switch the expiry check off.
        Assert.False(verifier.IsTrusted(assemblyPath: null, signedCms));
    }

    [Fact]
    public void IsTrusted_RejectsSigner_WhenTheRootIsNotAmongTheTrustAnchors()
    {
        using var pki = new AuthenticodeTestPki();
        using var otherPki = new AuthenticodeTestPki();
        using var signer = pki.IssueEndEntityCertificate("CN=SAF Authenticode foreign signer");
        var signedCms = CreateSignedCms(signer, pki.Intermediate);
        var verifier = new CrossPlatformAuthenticodeTrustVerifier(otherPki.TrustAnchors);

        // Guards the anchors themselves: a trusted verdict must come from the configured root and not
        // from the certificates the signature happens to carry.
        Assert.False(verifier.IsTrusted(assemblyPath: null, signedCms));
    }

    // Expires before now, but was valid a week ago, which is when the counter-signed tests date the
    // signature to.
    private static X509Certificate2 ExpiredSigner(AuthenticodeTestPki pki)
        => pki.IssueEndEntityCertificate(
            "CN=SAF Authenticode expired signer",
            notBefore: DateTimeOffset.UtcNow.AddDays(-10),
            notAfter: DateTimeOffset.UtcNow.AddDays(-1));

    internal static SignedCms CreateTimestampedCms(
        X509Certificate2 signer,
        X509Certificate2 timestampAuthority,
        DateTimeOffset timestamp,
        DateTimeOffset? counterSignatureTime = null,
        params X509Certificate2[] embedded)
    {
        var signedCms = new SignedCms(new ContentInfo([0x01, 0x02, 0x03, 0x04]), detached: false);
        signedCms.ComputeSignature(new CmsSigner(signer) { IncludeOption = X509IncludeOption.EndCertOnly });

        if (counterSignatureTime is not null)
        {
            AuthenticodeTestTimestamp.AddCounterSignature(signedCms, signer, counterSignatureTime.Value);
        }

        AuthenticodeTestTimestamp.AddRfc3161Timestamp(signedCms, timestampAuthority, timestamp);

        foreach (var certificate in embedded)
        {
            signedCms.AddCertificate(certificate);
        }

        return Reencode(signedCms);
    }

    internal static SignedCms CreateSignedCms(X509Certificate2 signer, params X509Certificate2[] embedded)
    {
        var signedCms = new SignedCms(new ContentInfo([0x01, 0x02, 0x03, 0x04]), detached: false);
        signedCms.ComputeSignature(new CmsSigner(signer) { IncludeOption = X509IncludeOption.EndCertOnly });
        foreach (var certificate in embedded)
        {
            signedCms.AddCertificate(certificate);
        }

        return Reencode(signedCms);
    }

    internal static SignedCms CreateCounterSignedCms(
        X509Certificate2 signer,
        DateTimeOffset signingTime,
        params X509Certificate2[] embedded)
    {
        var signedCms = new SignedCms(new ContentInfo([0x01, 0x02, 0x03, 0x04]), detached: false);
        signedCms.ComputeSignature(new CmsSigner(signer) { IncludeOption = X509IncludeOption.EndCertOnly });

        var counterSigner = new CmsSigner(signer) { IncludeOption = X509IncludeOption.EndCertOnly };
        counterSigner.SignedAttributes.Add(new Pkcs9SigningTime(signingTime.UtcDateTime));
        signedCms.SignerInfos[0].ComputeCounterSignature(counterSigner);

        foreach (var certificate in embedded)
        {
            signedCms.AddCertificate(certificate);
        }

        return Reencode(signedCms);
    }

    private static SignedCms Reencode(SignedCms signedCms)
    {
        var decoded = new SignedCms();
        decoded.Decode(signedCms.Encode());
        return decoded;
    }
}
