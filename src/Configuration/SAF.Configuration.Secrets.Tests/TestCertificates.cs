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
    /// for CMS decryption there). Dispose the returned certificate to release the key material.
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
}
