// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NSubstitute;
using SAF.Configuration.Secrets.Contracts;
using SAF.Configuration.Secrets.Protection;
using Xunit;

public sealed class SigningSecretProtectorTests : IDisposable
{
    private const string SecretName = "myapp/db/password";
    private const string RsaPssOid = "1.2.840.113549.1.1.10";
    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";

    private static readonly byte[] Plaintext = Encoding.UTF8.GetBytes("s3cr3t-value-äöü");

    private readonly List<X509Certificate2> _certificates = [];

    [Fact]
    public void Name_AppendsSignedToTheInnerName()
    {
        var protector = CreateProtector(CreateCertificate());

        Assert.Equal("pkcs+signed", protector.Name);
    }

    [Fact]
    public void ProtectThenUnprotect_RoundTripsSecret()
    {
        var protector = CreateProtector(CreateCertificate());

        var protectedData = protector.Protect(SecretName, Plaintext);

        Assert.Equal(Plaintext, protector.Unprotect(SecretName, protectedData));
    }

    [Fact]
    public void Protect_SignsWithRsaPssAndSha256_WithoutCarryingTheCertificate()
    {
        var protector = CreateProtector(CreateCertificate());

        var signed = Decode(protector.Protect(SecretName, Plaintext));

        var signerInfo = Assert.Single(signed.SignerInfos.Cast<SignerInfo>());
        Assert.Equal(RsaPssOid, signerInfo.SignatureAlgorithm.Value);
        Assert.Equal(Sha256Oid, signerInfo.DigestAlgorithm.Value);
        Assert.Empty(signed.Certificates);
    }

    [Fact]
    public void Unprotect_AcceptsTheNameInAnotherCase()
    {
        var protector = CreateProtector(CreateCertificate());

        var protectedData = protector.Protect("MyApp/Db/Password", Plaintext);

        Assert.Equal(Plaintext, protector.Unprotect(SecretName, protectedData));
    }

    [Fact]
    public void Unprotect_Rejects_AValueSignedForAnotherName()
    {
        var protector = CreateProtector(CreateCertificate());

        var protectedData = protector.Protect("myapp/db/user", Plaintext);

        var ex = Assert.Throws<CryptographicException>(() => protector.Unprotect(SecretName, protectedData));
        Assert.Contains(SecretName, ex.Message, StringComparison.Ordinal);
        Assert.Contains("myapp/db/user", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0x0000)]
    [InlineData(0xD800)]
    public void Protect_Rejects_ANameTheSignatureCannotCarryUnchanged(int character)
    {
        // The name attribute ends at NUL and replaces an unpaired surrogate.
        var protector = CreateProtector(CreateCertificate());

        var ex = Assert.Throws<ArgumentException>(() => protector.Protect("myapp/db" + (char)character + "x", Plaintext));
        Assert.Equal("name", ex.ParamName);
    }

    [Fact]
    public void Unprotect_Rejects_AChangedByteInTheProtectedValue()
    {
        var protector = CreateProtector(CreateCertificate());
        var protectedData = protector.Protect(SecretName, Plaintext);

        FlipByteWithin(protectedData, Decode(protectedData).ContentInfo.Content);

        var ex = Assert.Throws<CryptographicException>(() => protector.Unprotect(SecretName, protectedData));
        Assert.Contains(SecretName, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unprotect_Rejects_AChangedByteInTheSignature()
    {
        var protector = CreateProtector(CreateCertificate());
        var protectedData = protector.Protect(SecretName, Plaintext);

        FlipByteWithin(protectedData, Decode(protectedData).SignerInfos[0].GetSignature());

        Assert.Throws<CryptographicException>(() => protector.Unprotect(SecretName, protectedData));
    }

    [Fact]
    public void Unprotect_Rejects_AnUnsignedValue()
    {
        var certificate = CreateCertificate();
        var protector = CreateProtector(certificate);
        var unsigned = new PkcsSecretProtector(certificate).Protect(SecretName, Plaintext);

        var ex = Assert.Throws<CryptographicException>(() => protector.Unprotect(SecretName, unsigned));
        Assert.Contains("not signed", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unprotect_Rejects_ADetachedSignature()
    {
        // Decoded without its content, the value has none, so the digest cannot match.
        var certificate = CreateCertificate();
        var protector = CreateSigningOnlyProtector(certificate);
        using var key = certificate.GetRSAPrivateKey()!;

        var detached = Sign(Plaintext, Signer(certificate, key), detached: true, SecretName);

        var ex = Assert.Throws<CryptographicException>(() => protector.Unprotect(SecretName, detached));
        Assert.Contains(SecretName, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unprotect_Rejects_ASignatureWithoutAName()
    {
        var certificate = CreateCertificate();
        var protector = CreateSigningOnlyProtector(certificate);
        using var key = certificate.GetRSAPrivateKey()!;

        var unnamed = Sign(Plaintext, Signer(certificate, key), detached: false);

        var ex = Assert.Throws<CryptographicException>(() => protector.Unprotect(SecretName, unnamed));
        Assert.Contains("exactly one secret", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unprotect_Rejects_ASignatureWithTwoNames()
    {
        var certificate = CreateCertificate();
        var protector = CreateSigningOnlyProtector(certificate);
        using var key = certificate.GetRSAPrivateKey()!;

        var twoNames = Sign(Plaintext, Signer(certificate, key), detached: false, SecretName, SecretName);

        var ex = Assert.Throws<CryptographicException>(() => protector.Unprotect(SecretName, twoNames));
        Assert.Contains("exactly one secret", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unprotect_Rejects_AValueThatCarriesACertificate_EvenWithAValidSignature()
    {
        var certificate = CreateCertificate();
        var protector = CreateSigningOnlyProtector(certificate);
        using var key = certificate.GetRSAPrivateKey()!;

        var carrying = Sign(Plaintext, Signer(certificate, key, X509IncludeOption.EndCertOnly), detached: false, SecretName);

        var ex = Assert.Throws<CryptographicException>(() => protector.Unprotect(SecretName, carrying));
        Assert.Contains("carries certificates", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unprotect_Rejects_AValueWithoutASignature()
    {
        var protector = CreateSigningOnlyProtector(CreateCertificate());

        var hashOnly = Sign(Plaintext, new CmsSigner(SubjectIdentifierType.NoSignature), detached: false, SecretName);

        var ex = Assert.Throws<CryptographicException>(() => protector.Unprotect(SecretName, hashOnly));
        Assert.Contains(SecretName, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unprotect_Rejects_ASignerIdentifiedByItsSubjectKeyIdentifier()
    {
        // The protector writes issuer and serial number only.
        var certificate = CreateCertificate();
        var protector = CreateSigningOnlyProtector(certificate);
        using var key = certificate.GetRSAPrivateKey()!;
        var signer = new CmsSigner(SubjectIdentifierType.SubjectKeyIdentifier, certificate, key, RSASignaturePadding.Pss)
        {
            IncludeOption = X509IncludeOption.None
        };

        var bySubjectKey = Sign(Plaintext, signer, detached: false, SecretName);

        var ex = Assert.Throws<CryptographicException>(() => protector.Unprotect(SecretName, bySubjectKey));
        Assert.Contains(certificate.Thumbprint, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(X509IncludeOption.None)]
    [InlineData(X509IncludeOption.EndCertOnly)]
    public void Unprotect_Rejects_AForeignKeyPosingAsTheOwnCertificate(X509IncludeOption includeOption)
    {
        var certificate = CreateCertificate();
        var protector = CreateSigningOnlyProtector(certificate);

        // Same subject, issuer and serial number as the own certificate, but another key.
        using var foreignKey = RSA.Create(2048);
        using var impostor = new CertificateRequest(
                certificate.SubjectName, foreignKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .Create(
                certificate.IssuerName,
                X509SignatureGenerator.CreateForRSA(foreignKey, RSASignaturePadding.Pkcs1),
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddHours(1),
                certificate.SerialNumberBytes.ToArray());

        var forged = Sign(Plaintext, Signer(impostor, foreignKey, includeOption), detached: false, SecretName);

        Assert.True(Decode(forged).SignerInfos[0].SignerIdentifier.MatchesCertificate(certificate));
        Assert.Throws<CryptographicException>(() => protector.Unprotect(SecretName, forged));
    }

    [Fact]
    public void Unprotect_Rejects_AValueSignedWithAnotherCertificate()
    {
        var writer = CreateSigningOnlyProtector(CreateCertificate());
        var certificate = CreateCertificate();
        var reader = CreateSigningOnlyProtector(certificate);

        var protectedData = writer.Protect(SecretName, Plaintext);

        var ex = Assert.Throws<CryptographicException>(() => reader.Unprotect(SecretName, protectedData));
        Assert.Contains(certificate.Thumbprint, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unprotect_Rejects_ASecondSignature()
    {
        var protector = CreateProtector(CreateCertificate());
        var signed = Decode(protector.Protect(SecretName, Plaintext));

        var other = CreateCertificate();
        using var otherKey = other.GetRSAPrivateKey()!;
        signed.ComputeSignature(Signer(other, otherKey), silent: true);

        var ex = Assert.Throws<CryptographicException>(() => protector.Unprotect(SecretName, signed.Encode()));
        Assert.Contains("exactly one", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Protect_Throws_WithClearMessage_WhenTheCertificateHasNoPrivateKey()
    {
        var certificate = CreateCertificate();
        using var publicOnly = X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert));
        var protector = CreateSigningOnlyProtector(publicOnly);

        var ex = Assert.Throws<InvalidOperationException>(() => protector.Protect(SecretName, Plaintext));
        Assert.Contains("private key", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Protect_Throws_NamingTheCertificate_WhenTheKeyMayOnlyDecrypt()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Key usage restrictions of CNG keys exist on Windows only.");
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // A named key: CopyWithPrivateKey would export an ephemeral one and lose the restriction.
        var parameters = new CngKeyCreationParameters { KeyUsage = CngKeyUsages.Decryption };
        parameters.Parameters.Add(new CngProperty("Length", BitConverter.GetBytes(2048), CngPropertyOptions.None));
        using var cngKey = CngKey.Create(CngAlgorithm.Rsa, $"saf-secret-store-test-{Guid.NewGuid():N}", parameters);
        try
        {
            using var decryptOnlyKey = new RSACng(cngKey);
            using var issuerKey = RSA.Create(2048);
            using var publicPart = new CertificateRequest(
                    "CN=SAF Secret Store Test", decryptOnlyKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                .Create(
                    new X500DistinguishedName("CN=SAF Secret Store Test Issuer"),
                    X509SignatureGenerator.CreateForRSA(issuerKey, RSASignaturePadding.Pkcs1),
                    DateTimeOffset.UtcNow.AddMinutes(-5),
                    DateTimeOffset.UtcNow.AddHours(1),
                    [1, 2, 3, 4]);
            using var protector = CreateSigningOnlyProtector(publicPart.CopyWithPrivateKey(decryptOnlyKey));

            var ex = Assert.Throws<CryptographicException>(() => protector.Protect(SecretName, Plaintext));
            Assert.Contains(SecretName, ex.Message, StringComparison.Ordinal);
            Assert.Contains(publicPart.Thumbprint, ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            cngKey.Delete();
        }
    }

    [Fact]
    public void Unprotect_VerifiesWithThePublicKeyOnly()
    {
        var certificate = CreateCertificate();
        var writer = CreateSigningOnlyProtector(certificate);
        using var publicOnly = X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert));
        var reader = CreateSigningOnlyProtector(publicOnly);

        var protectedData = writer.Protect(SecretName, Plaintext);

        Assert.Equal(Plaintext, reader.Unprotect(SecretName, protectedData));
    }

    [Fact]
    public void ProtectThenUnprotect_RoundTrips_WithAnExpiredCertificate()
    {
        var certificate = TestCertificates.CreateRsaCertificate(
            DateTimeOffset.UtcNow.AddYears(-2), DateTimeOffset.UtcNow.AddYears(-1));
        _certificates.Add(certificate);
        Assert.True(certificate.NotAfter < DateTime.Now);
        var protector = CreateProtector(certificate);

        var protectedData = protector.Protect(SecretName, Plaintext);

        Assert.Equal(Plaintext, protector.Unprotect(SecretName, protectedData));
    }

    [Fact]
    public void ProtectThenUnprotect_RoundTrips_WithAKeyUsageThatAllowsSignatures()
    {
        using var certificate = CreateEphemeralCertificate(X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature);
        var protector = CreateSigningOnlyProtector(certificate);

        var protectedData = protector.Protect(SecretName, Plaintext);

        Assert.Equal(Plaintext, protector.Unprotect(SecretName, protectedData));
    }

    [Fact]
    public void Constructor_Throws_ForAKeyUsageWithoutDigitalSignature()
    {
        using var certificate = CreateEphemeralCertificate(X509KeyUsageFlags.KeyEncipherment);

        var ex = Assert.Throws<ArgumentException>(() => CreateSigningOnlyProtector(certificate));
        Assert.Contains("digitalSignature", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_Throws_OnNullArguments()
    {
        using var certificate = CreateEphemeralCertificate();

        Assert.Throws<ArgumentNullException>(() => new SigningSecretProtector(null!, certificate));
        Assert.Throws<ArgumentNullException>(() => new SigningSecretProtector(new ReversingSecretProtector(), null!));
    }

    [Fact]
    public void Constructor_Throws_ForACertificateWithoutAnRsaKey()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var certificate = new CertificateRequest("CN=SAF Secret Store Test", key, HashAlgorithmName.SHA256)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));

        var ex = Assert.Throws<ArgumentException>(() => CreateSigningOnlyProtector(certificate));
        Assert.Contains("RSA", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ProtectAndUnprotect_Throw_OnAMissingName(string? name)
    {
        using var certificate = CreateEphemeralCertificate();
        var protector = CreateSigningOnlyProtector(certificate);

        Assert.ThrowsAny<ArgumentException>(() => protector.Protect(name!, Plaintext));
        Assert.ThrowsAny<ArgumentException>(() => protector.Unprotect(name!, Plaintext));
    }

    [Fact]
    public void Dispose_DisposesTheInnerProtectorAndTheCertificate()
    {
        var inner = Substitute.For<ISecretProtector, IDisposable>();
        var certificate = CreateEphemeralCertificate();
        var protector = new SigningSecretProtector(inner, certificate);

        protector.Dispose();

        ((IDisposable)inner).Received(1).Dispose();
        Assert.Throws<CryptographicException>(() => certificate.Export(X509ContentType.Cert));
    }

    [Fact]
    public void Protect_Throws_AfterDispose()
    {
        var protector = CreateSigningOnlyProtector(CreateEphemeralCertificate());
        protector.Dispose();

        Assert.Throws<ObjectDisposedException>(() => protector.Protect(SecretName, Plaintext));
    }

    public void Dispose()
    {
        foreach (var certificate in _certificates)
        {
            TestCertificates.DisposeAndDeleteKey(certificate);
        }
    }

    private static SigningSecretProtector CreateProtector(X509Certificate2 certificate)
        => new(new PkcsSecretProtector(certificate), certificate);

    // For tests about the signature alone; the inner protector needs no key.
    private static SigningSecretProtector CreateSigningOnlyProtector(X509Certificate2 certificate)
        => new(new ReversingSecretProtector("pkcs"), certificate);

    // Not persisted, so a test that lets the protector dispose it leaves no key file behind.
    private static X509Certificate2 CreateEphemeralCertificate(X509KeyUsageFlags? keyUsage = null)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=SAF Secret Store Test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        if (keyUsage is { } usage)
        {
            request.CertificateExtensions.Add(new X509KeyUsageExtension(usage, critical: true));
        }

        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));
    }

    private static SignedCms Decode(byte[] protectedData)
    {
        var signed = new SignedCms();
        signed.Decode(protectedData);
        return signed;
    }

    private static CmsSigner Signer(
        X509Certificate2 certificate, RSA key, X509IncludeOption includeOption = X509IncludeOption.None)
        => new(SubjectIdentifierType.IssuerAndSerialNumber, certificate, key, RSASignaturePadding.Pss)
        {
            IncludeOption = includeOption
        };

    private static byte[] Sign(byte[] plaintext, CmsSigner signer, bool detached, params string[] names)
    {
        foreach (var name in names)
        {
            signer.SignedAttributes.Add(new Pkcs9DocumentName(name));
        }

        var signed = new SignedCms(new ContentInfo(new ReversingSecretProtector().Protect(SecretName, plaintext)), detached);
        signed.ComputeSignature(signer, silent: true);
        return signed.Encode();
    }

    private static void FlipByteWithin(byte[] message, byte[] part)
    {
        var offset = message.AsSpan().IndexOf(part);
        Assert.True(offset >= 0, "The part does not occur in the message.");
        message[offset + (part.Length / 2)] ^= 0x01;
    }

    private X509Certificate2 CreateCertificate()
    {
        var certificate = TestCertificates.CreateRsaCertificate();
        _certificates.Add(certificate);
        return certificate;
    }
}
