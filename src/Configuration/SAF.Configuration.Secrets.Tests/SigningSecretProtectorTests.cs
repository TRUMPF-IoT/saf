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
        // The store matches its keys case-insensitively, so a name differing only in case is the same entry.
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
        Assert.Contains(SecretName, ex.Message);
        Assert.Contains("myapp/db/user", ex.Message);
    }

    [Fact]
    public void Unprotect_Rejects_AChangedByteInTheProtectedValue()
    {
        var protector = CreateProtector(CreateCertificate());
        var protectedData = protector.Protect(SecretName, Plaintext);

        FlipByteWithin(protectedData, Decode(protectedData).ContentInfo.Content);

        var ex = Assert.Throws<CryptographicException>(() => protector.Unprotect(SecretName, protectedData));
        Assert.Contains(SecretName, ex.Message);
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
        // Accepting what the inner protector wrote directly would let anyone with the public key bypass the
        // signature, exactly like the inner protector alone.
        var certificate = CreateCertificate();
        var protector = CreateProtector(certificate);
        var unsigned = new PkcsSecretProtector(certificate).Protect(Plaintext);

        var ex = Assert.Throws<CryptographicException>(() => protector.Unprotect(SecretName, unsigned));
        Assert.Contains("not signed", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(X509IncludeOption.None)]
    [InlineData(X509IncludeOption.EndCertOnly)]
    public void Unprotect_Rejects_AForeignKeyPosingAsTheOwnCertificate(X509IncludeOption includeOption)
    {
        var certificate = CreateCertificate();
        var protector = new SigningSecretProtector(new ReversingSecretProtector("pkcs"), certificate);

        // Same subject, issuer and serial number as the own certificate, but another key. Carried along in the
        // message, it would be the certificate CheckSignature verifies against.
        using var foreignKey = RSA.Create(2048);
        using var impostor = new CertificateRequest(
                certificate.SubjectName, foreignKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .Create(
                certificate.IssuerName,
                X509SignatureGenerator.CreateForRSA(foreignKey, RSASignaturePadding.Pkcs1),
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddHours(1),
                certificate.SerialNumberBytes.ToArray());

        var forged = Sign(new ReversingSecretProtector().Protect(Plaintext), impostor, foreignKey, includeOption);

        Assert.True(Decode(forged).SignerInfos[0].SignerIdentifier.MatchesCertificate(certificate));
        Assert.Throws<CryptographicException>(() => protector.Unprotect(SecretName, forged));
    }

    [Fact]
    public void Unprotect_Rejects_AValueSignedWithAnotherCertificate()
    {
        var writer = new SigningSecretProtector(new ReversingSecretProtector("pkcs"), CreateCertificate());
        var certificate = CreateCertificate();
        var reader = new SigningSecretProtector(new ReversingSecretProtector("pkcs"), certificate);

        var protectedData = writer.Protect(SecretName, Plaintext);

        var ex = Assert.Throws<CryptographicException>(() => reader.Unprotect(SecretName, protectedData));
        Assert.Contains(certificate.Thumbprint, ex.Message);
    }

    [Fact]
    public void Unprotect_Rejects_ASecondSignature()
    {
        var protector = CreateProtector(CreateCertificate());
        var signed = Decode(protector.Protect(SecretName, Plaintext));

        var other = CreateCertificate();
        using var otherKey = other.GetRSAPrivateKey()!;
        signed.ComputeSignature(
            new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, other, otherKey, RSASignaturePadding.Pss)
            {
                IncludeOption = X509IncludeOption.None
            },
            silent: true);

        var ex = Assert.Throws<CryptographicException>(() => protector.Unprotect(SecretName, signed.Encode()));
        Assert.Contains("exactly one", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Protect_Throws_WithClearMessage_WhenTheCertificateHasNoPrivateKey()
    {
        var certificate = CreateCertificate();
        using var publicOnly = X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert));
        var protector = new SigningSecretProtector(new ReversingSecretProtector("pkcs"), publicOnly);

        var ex = Assert.Throws<InvalidOperationException>(() => protector.Protect(SecretName, Plaintext));
        Assert.Contains("private key", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unprotect_VerifiesWithThePublicKeyOnly()
    {
        var certificate = CreateCertificate();
        var writer = new SigningSecretProtector(new ReversingSecretProtector("pkcs"), certificate);
        using var publicOnly = X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert));
        var reader = new SigningSecretProtector(new ReversingSecretProtector("pkcs"), publicOnly);

        var protectedData = writer.Protect(SecretName, Plaintext);

        Assert.Equal(Plaintext, reader.Unprotect(SecretName, protectedData));
    }

    [Fact]
    public void ProtectThenUnprotect_RoundTrips_WithAnExpiredCertificate()
    {
        // Nothing checks the validity period: an expired store certificate must not make its secrets unreadable.
        var certificate = TestCertificates.CreateRsaCertificate(
            DateTimeOffset.UtcNow.AddYears(-2), DateTimeOffset.UtcNow.AddYears(-1));
        _certificates.Add(certificate);
        Assert.True(certificate.NotAfter < DateTime.Now);
        var protector = CreateProtector(certificate);

        var protectedData = protector.Protect(SecretName, Plaintext);

        Assert.Equal(Plaintext, protector.Unprotect(SecretName, protectedData));
    }

    [Fact]
    public void ProtectAndUnprotect_WithoutAName_AreNotSupported()
    {
        // Through the interface as well: a caller holding an ISecretProtector must not get an unbound value.
        ISecretProtector protector = CreateProtector(CreateCertificate());

        Assert.Throws<NotSupportedException>(() => protector.Protect(Plaintext));
        Assert.Throws<NotSupportedException>(() => protector.Unprotect(Plaintext));
    }

    [Fact]
    public void Constructor_Throws_OnNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new SigningSecretProtector(null!, CreateCertificate()));
        Assert.Throws<ArgumentNullException>(() => new SigningSecretProtector(new ReversingSecretProtector(), null!));
    }

    [Fact]
    public void Constructor_Throws_ForACertificateWithoutAnRsaKey()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var certificate = new CertificateRequest("CN=SAF Secret Store Test", key, HashAlgorithmName.SHA256)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));

        var ex = Assert.Throws<ArgumentException>(
            () => new SigningSecretProtector(new ReversingSecretProtector(), certificate));
        Assert.Contains("RSA", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ProtectAndUnprotect_Throw_OnAMissingName(string? name)
    {
        var protector = CreateProtector(CreateCertificate());

        Assert.ThrowsAny<ArgumentException>(() => protector.Protect(name!, Plaintext));
        Assert.ThrowsAny<ArgumentException>(() => protector.Unprotect(name!, Plaintext));
    }

    [Fact]
    public void Dispose_DisposesTheInnerProtectorAndTheCertificate()
    {
        var inner = Substitute.For<ISecretProtector, IDisposable>();
        var certificate = CreateCertificate();
        var protector = new SigningSecretProtector(inner, certificate);

        protector.Dispose();

        ((IDisposable)inner).Received(1).Dispose();
        Assert.Throws<CryptographicException>(() => certificate.Export(X509ContentType.Cert));
    }

    [Fact]
    public void Protect_Throws_AfterDispose()
    {
        var protector = CreateProtector(CreateCertificate());
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

    private static SignedCms Decode(byte[] protectedData)
    {
        var signed = new SignedCms();
        signed.Decode(protectedData);
        return signed;
    }

    private static byte[] Sign(byte[] content, X509Certificate2 certificate, RSA key, X509IncludeOption includeOption)
    {
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate, key, RSASignaturePadding.Pss)
        {
            IncludeOption = includeOption
        };
        signer.SignedAttributes.Add(new Pkcs9DocumentName(SecretName));

        var signed = new SignedCms(new ContentInfo(content), detached: false);
        signed.ComputeSignature(signer, silent: true);
        return signed.Encode();
    }

    // Flips a byte inside the part of the message that holds `part`, which the encoding carries verbatim.
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
