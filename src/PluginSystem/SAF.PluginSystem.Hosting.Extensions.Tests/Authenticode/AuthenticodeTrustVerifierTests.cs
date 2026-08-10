// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests.Authenticode;

using SAF.PluginSystem.Hosting.Extensions.Authenticode;
using System.Security.Cryptography;
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
    public void CrossPlatformVerifier_RejectsSelfSignedCodeSigningCertificate()
    {
        using var certificate = CreateSelfSignedCodeSigningCertificate();
        var verifier = new CrossPlatformAuthenticodeTrustVerifier();

        var isTrusted = verifier.IsTrusted("unused.dll", certificate);

        Assert.False(isTrusted);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void WindowsVerifier_ClaimsFileIntegrity()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "This test covers the Windows trust verifier.");
        var verifier = new WindowsAuthenticodeTrustVerifier();

        Assert.True(verifier.VerifiesFileIntegrity);
    }

    private static X509Certificate2 CreateSelfSignedCodeSigningCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=SAF Authenticode test",
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