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

public class PkcsSecretProtectorTests
{
    [Fact]
    public void Name_IsStableProtectorIdentifier()
    {
        using var certificate = CreateCertificate();

        Assert.Equal("pkcs", new PkcsSecretProtector(certificate).Name);
        Assert.Equal(PkcsSecretProtector.ProtectorName, new PkcsSecretProtector(certificate).Name);
    }

    [Fact]
    public void ProtectThenUnprotect_RoundTripsSecret()
    {
        using var certificate = CreateCertificate();
        var protector = new PkcsSecretProtector(certificate);
        var plaintext = Encoding.UTF8.GetBytes("s3cr3t-value-äöü");

        var protectedData = protector.Protect(plaintext);
        var recovered = protector.Unprotect(protectedData);

        Assert.Equal(plaintext, recovered);
    }

    [Fact]
    public void Protect_DoesNotLeakPlaintext()
    {
        using var certificate = CreateCertificate();
        var protector = new PkcsSecretProtector(certificate);
        var plaintext = Encoding.UTF8.GetBytes("do-not-leak-me");

        var protectedData = protector.Protect(plaintext);

        Assert.NotEqual(plaintext, protectedData);
        Assert.False(ContainsSubsequence(protectedData, plaintext));
    }

    [Fact]
    public void Protect_ProducesDifferentCiphertextEachTime()
    {
        using var certificate = CreateCertificate();
        var protector = new PkcsSecretProtector(certificate);
        var plaintext = Encoding.UTF8.GetBytes("same-input");

        var first = protector.Protect(plaintext);
        var second = protector.Protect(plaintext);

        // A fresh content key / IV per call must yield distinct envelopes for identical plaintext.
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Unprotect_Fails_WithDifferentCertificate()
    {
        using var writerCertificate = CreateCertificate();
        using var otherCertificate = CreateCertificate();
        var writer = new PkcsSecretProtector(writerCertificate);
        var reader = new PkcsSecretProtector(otherCertificate);

        var protectedData = writer.Protect(Encoding.UTF8.GetBytes("value"));

        Assert.ThrowsAny<CryptographicException>(() => reader.Unprotect(protectedData));
    }

    [Fact]
    public void Unprotect_Throws_OnCorruptedPayload()
    {
        using var certificate = CreateCertificate();
        var protector = new PkcsSecretProtector(certificate);

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect([1, 2, 3, 4]));
    }

    [Fact]
    public void Constructor_Throws_OnNullCertificate()
        => Assert.Throws<ArgumentNullException>(() => new PkcsSecretProtector(null!));

    [Fact]
    public void Protect_Throws_OnNullPlaintext()
    {
        using var certificate = CreateCertificate();
        var protector = new PkcsSecretProtector(certificate);

        Assert.Throws<ArgumentNullException>(() => protector.Protect(null!));
    }

    [Fact]
    public void Unprotect_Throws_OnNullPayload()
    {
        using var certificate = CreateCertificate();
        var protector = new PkcsSecretProtector(certificate);

        Assert.Throws<ArgumentNullException>(() => protector.Unprotect(null!));
    }

    // Creates an ephemeral self-signed RSA certificate whose private key is usable by CMS on every
    // platform. The key is round-tripped through a PFX so Windows CNG exposes a decryptable key handle.
    private static X509Certificate2 CreateCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=SAF Secret Store Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var ephemeral = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));

        var pfx = ephemeral.Export(X509ContentType.Pfx);
        return X509CertificateLoader.LoadPkcs12(pfx, password: null, X509KeyStorageFlags.Exportable);
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
