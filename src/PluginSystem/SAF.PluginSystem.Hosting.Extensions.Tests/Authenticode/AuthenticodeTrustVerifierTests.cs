// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests.Authenticode;

using SAF.PluginSystem.Hosting.Extensions.Authenticode;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.Versioning;

public sealed class AuthenticodeTrustVerifierTests
{
    [Fact]
    public void CrossPlatformVerifier_DoesNotClaimFileIntegrity()
    {
        var verifier = new CrossPlatformAuthenticodeTrustVerifier();

        Assert.False(verifier.VerifiesFileIntegrity);
    }

    [Fact]
    public void CrossPlatformVerifier_DoesNotRequireAFilePath()
    {
        var verifier = new CrossPlatformAuthenticodeTrustVerifier();

        Assert.False(verifier.RequiresFilePath);
    }

    [Fact]
    public void CrossPlatformVerifier_RejectsSelfSignedCodeSigningCertificate()
    {
        using var certificate = CreateSelfSignedCodeSigningCertificate();
        var signedCms = CreateSignedCms(certificate);
        var verifier = new CrossPlatformAuthenticodeTrustVerifier();

        Assert.False(verifier.IsTrusted(assemblyPath: null, signedCms));
    }

    [Fact]
    public void CrossPlatformVerifier_RejectsSignature_WithoutSignerInfo()
    {
        var verifier = new CrossPlatformAuthenticodeTrustVerifier();

        Assert.False(verifier.IsTrusted(assemblyPath: null, new SignedCms()));
    }

    [Fact]
    public void ChainPolicy_TakesEmbeddedCertificatesIntoTheExtraStore()
    {
        using var signerCertificate = CreateSelfSignedCodeSigningCertificate();
        using var embeddedCertificate = CreateSelfSignedCodeSigningCertificate("CN=SAF Authenticode intermediate");
        var signedCms = CreateSignedCms(signerCertificate, embeddedCertificate);

        var policy = new CrossPlatformAuthenticodeTrustVerifier().CreateChainPolicy(signedCms, signedCms.SignerInfos[0]);

        // Without these the chain cannot get past the leaf on a host that cannot fetch the issuer via AIA.
        var extraStoreThumbprints = policy.ExtraStore.Cast<X509Certificate2>().Select(c => c.Thumbprint).ToList();
        Assert.Contains(signerCertificate.Thumbprint, extraStoreThumbprints);
        Assert.Contains(embeddedCertificate.Thumbprint, extraStoreThumbprints);
    }

    [Fact]
    public void ChainPolicy_RequiresTheCodeSigningUsage()
    {
        using var certificate = CreateSelfSignedCodeSigningCertificate();
        var signedCms = CreateSignedCms(certificate);

        var policy = new CrossPlatformAuthenticodeTrustVerifier().CreateChainPolicy(signedCms, signedCms.SignerInfos[0]);

        Assert.Contains(policy.ApplicationPolicy.Cast<Oid>(), oid => oid.Value == "1.3.6.1.5.5.7.3.3");
        Assert.Equal(X509RevocationMode.NoCheck, policy.RevocationMode);
    }

    [Fact]
    public void ChainPolicy_VerifiesAgainstTheCounterSignatureTime_WhenTheSignatureIsTimestamped()
    {
        var signingTime = new DateTimeOffset(2021, 6, 15, 10, 30, 0, TimeSpan.Zero);
        using var certificate = CreateSelfSignedCodeSigningCertificate();
        var signedCms = CreateCounterSignedCms(certificate, signingTime);

        var policy = new CrossPlatformAuthenticodeTrustVerifier().CreateChainPolicy(signedCms, signedCms.SignerInfos[0]);

        // Otherwise a certificate that expired after signing would invalidate an intact signature,
        // which is not what WinVerifyTrust does on Windows.
        Assert.Equal(signingTime.UtcDateTime, policy.VerificationTime.ToUniversalTime());
    }

    [Fact]
    public void ChainPolicy_VerifiesAgainstTheCurrentTime_WhenTheSignatureIsNotTimestamped()
    {
        using var certificate = CreateSelfSignedCodeSigningCertificate();
        var signedCms = CreateSignedCms(certificate);

        var policy = new CrossPlatformAuthenticodeTrustVerifier().CreateChainPolicy(signedCms, signedCms.SignerInfos[0]);

        Assert.InRange(
            policy.VerificationTime.ToUniversalTime(),
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddMinutes(5));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void WindowsVerifier_ClaimsFileIntegrityAndRequiresAFilePath()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "This test covers the Windows trust verifier.");
        var verifier = new WindowsAuthenticodeTrustVerifier();

        Assert.True(verifier.VerifiesFileIntegrity);
        Assert.True(verifier.RequiresFilePath);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void WindowsVerifier_RejectsCallWithoutAFilePath()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "This test covers the Windows trust verifier.");
        var verifier = new WindowsAuthenticodeTrustVerifier();

        Assert.Throws<ArgumentNullException>(() => verifier.IsTrusted(null, new SignedCms()));
    }

    private static SignedCms CreateSignedCms(X509Certificate2 signerCertificate, params X509Certificate2[] embeddedCertificates)
    {
        var signedCms = new SignedCms(new ContentInfo([0x01, 0x02, 0x03, 0x04]), detached: false);
        signedCms.ComputeSignature(new CmsSigner(signerCertificate) { IncludeOption = X509IncludeOption.EndCertOnly });

        foreach (var certificate in embeddedCertificates)
        {
            signedCms.AddCertificate(certificate);
        }

        return Reencode(signedCms);
    }

    private static SignedCms CreateCounterSignedCms(X509Certificate2 signerCertificate, DateTimeOffset signingTime)
    {
        var signedCms = new SignedCms(new ContentInfo([0x01, 0x02, 0x03, 0x04]), detached: false);
        signedCms.ComputeSignature(new CmsSigner(signerCertificate) { IncludeOption = X509IncludeOption.EndCertOnly });

        // The legacy Authenticode timestamp: a PKCS#9 counter-signature carrying the signing time.
        var counterSigner = new CmsSigner(signerCertificate) { IncludeOption = X509IncludeOption.EndCertOnly };
        counterSigner.SignedAttributes.Add(new Pkcs9SigningTime(signingTime.UtcDateTime));
        signedCms.SignerInfos[0].ComputeCounterSignature(counterSigner);

        return Reencode(signedCms);
    }

    private static SignedCms Reencode(SignedCms signedCms)
    {
        var decoded = new SignedCms();
        decoded.Decode(signedCms.Encode());
        return decoded;
    }

    private static X509Certificate2 CreateSelfSignedCodeSigningCertificate(string subject = "CN=SAF Authenticode test")
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            subject,
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            [new Oid("1.3.6.1.5.5.7.3.3")],
            critical: false));

        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(5));
    }
}
