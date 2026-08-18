// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Test helpers for producing throwaway certificates usable by PKCS#7/CMS enveloping.
/// </summary>
internal static class TestCertificates
{
    /// <summary>
    /// Creates an ephemeral self-signed RSA certificate whose private key is usable by CMS on every
    /// platform. The key is round-tripped through a PFX so Windows CNG exposes a decryptable key handle
    /// (ephemeral keys straight from <see cref="CertificateRequest.CreateSelfSigned"/> cannot be used
    /// for CMS decryption there). That round-trip persists the private key into the Windows CNG key
    /// store as a side effect of loading — call <see cref="DisposeAndDeleteKey"/> instead of a plain
    /// <c>Dispose()</c> to also remove it, or repeated test runs accumulate key files.
    /// </summary>
    public static X509Certificate2 CreateRsaCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=SAF Secret Store Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var ephemeral = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));

        var pfx = ephemeral.Export(X509ContentType.Pfx);
        return X509CertificateLoader.LoadPkcs12(pfx, password: null, X509KeyStorageFlags.Exportable);
    }

    /// <summary>
    /// Disposes a certificate created by <see cref="CreateRsaCertificate"/> and deletes the CNG key
    /// file it persisted on Windows, so test runs don't leave key material under
    /// <c>%APPDATA%\Microsoft\Crypto\Keys</c>.
    /// </summary>
    public static void DisposeAndDeleteKey(X509Certificate2 certificate)
    {
        try
        {
            using var rsaKey = certificate.GetRSAPrivateKey();
            if (OperatingSystem.IsWindows() && rsaKey is RSACng rsaCng)
            {
                rsaCng.Key.Delete();
            }
        }
        catch (CryptographicException)
        {
            // Already disposed by the code under test — nothing left to clean up.
        }

        certificate.Dispose();
    }
}
