// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using SAF.Configuration.Secrets.Contracts;
using SAF.Configuration.Secrets.Protection;
using Xunit;

public class PkcsSecretProtectorTests : IDisposable
{
    private readonly List<X509Certificate2> _certificates = [];

    [Fact]
    public void Name_IsStableProtectorIdentifier()
    {
        var certificate = CreateCertificate();

        Assert.Equal("pkcs", new PkcsSecretProtector(certificate).Name);
        Assert.Equal(PkcsSecretProtector.ProtectorName, new PkcsSecretProtector(CreateCertificate()).Name);
    }

    [Fact]
    public void ProtectThenUnprotect_RoundTripsSecret()
    {
        var protector = new PkcsSecretProtector(CreateCertificate());
        var plaintext = Encoding.UTF8.GetBytes("s3cr3t-value-äöü");

        var protectedData = protector.Protect(plaintext);
        var recovered = protector.Unprotect(protectedData);

        Assert.Equal(plaintext, recovered);
    }

    [Fact]
    public void Protect_DoesNotLeakPlaintext()
    {
        var protector = new PkcsSecretProtector(CreateCertificate());
        var plaintext = Encoding.UTF8.GetBytes("do-not-leak-me");

        var protectedData = protector.Protect(plaintext);

        Assert.NotEqual(plaintext, protectedData);
        Assert.False(ContainsSubsequence(protectedData, plaintext));
    }

    [Fact]
    public void Protect_ProducesDifferentCiphertextEachTime()
    {
        var protector = new PkcsSecretProtector(CreateCertificate());
        var plaintext = Encoding.UTF8.GetBytes("same-input");

        var first = protector.Protect(plaintext);
        var second = protector.Protect(plaintext);

        // A fresh content key / IV per call must yield distinct envelopes for identical plaintext.
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Unprotect_Fails_WithDifferentCertificate()
    {
        var writer = new PkcsSecretProtector(CreateCertificate());
        var reader = new PkcsSecretProtector(CreateCertificate());

        var protectedData = writer.Protect(Encoding.UTF8.GetBytes("value"));

        Assert.ThrowsAny<CryptographicException>(() => reader.Unprotect(protectedData));
    }

    [Fact]
    public void Unprotect_Throws_OnCorruptedPayload()
    {
        var protector = new PkcsSecretProtector(CreateCertificate());

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect([1, 2, 3, 4]));
    }

    [Fact]
    public void Unprotect_Throws_WithClearMessage_WhenCertificateHasNoPrivateKey()
    {
        var writerCertificate = CreateCertificate();
        var writer = new PkcsSecretProtector(writerCertificate);
        var protectedData = writer.Protect(Encoding.UTF8.GetBytes("value"));

        using var publicOnlyCertificate = X509CertificateLoader.LoadCertificate(writerCertificate.Export(X509ContentType.Cert));
        var reader = new PkcsSecretProtector(publicOnlyCertificate);

        var ex = Assert.Throws<InvalidOperationException>(() => reader.Unprotect(protectedData));
        Assert.Contains("private key", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Protect_Succeeds_WithPublicKeyOnlyCertificate()
    {
        var writerCertificate = CreateCertificate();
        using var publicOnlyCertificate = X509CertificateLoader.LoadCertificate(writerCertificate.Export(X509ContentType.Cert));
        var protector = new PkcsSecretProtector(publicOnlyCertificate);

        var protectedData = protector.Protect(Encoding.UTF8.GetBytes("value"));

        Assert.NotEmpty(protectedData);
    }

    [Fact]
    public void Dispose_DisposesTheCertificate()
    {
        var certificate = CreateCertificate();
        var protector = new PkcsSecretProtector(certificate);

        protector.Dispose();

        Assert.Throws<CryptographicException>(() => certificate.Export(X509ContentType.Cert));
    }

    [Fact]
    public void Protect_Throws_AfterDispose()
    {
        var protector = new PkcsSecretProtector(CreateCertificate());
        protector.Dispose();

        Assert.Throws<ObjectDisposedException>(() => protector.Protect(Encoding.UTF8.GetBytes("value")));
    }

    [Fact]
    public void Constructor_Throws_OnNullCertificate()
        => Assert.Throws<ArgumentNullException>(() => new PkcsSecretProtector(null!));

    [Fact]
    public void Protect_Throws_OnNullPlaintext()
    {
        var protector = new PkcsSecretProtector(CreateCertificate());

        Assert.Throws<ArgumentNullException>(() => protector.Protect(null!));
    }

    [Fact]
    public void Unprotect_Throws_OnNullPayload()
    {
        var protector = new PkcsSecretProtector(CreateCertificate());

        Assert.Throws<ArgumentNullException>(() => protector.Unprotect(null!));
    }

    public void Dispose()
    {
        foreach (var certificate in _certificates)
        {
            TestCertificates.DisposeAndDeleteKey(certificate);
        }
    }

    private X509Certificate2 CreateCertificate()
    {
        var certificate = TestCertificates.CreateRsaCertificate();
        _certificates.Add(certificate);
        return certificate;
    }

    private static bool ContainsSubsequence(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || needle.Length > haystack.Length)
        {
            return false;
        }

        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }
}
