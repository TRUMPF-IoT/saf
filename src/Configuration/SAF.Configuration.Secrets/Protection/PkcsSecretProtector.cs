// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Protection;

using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// The default, cross-platform <see cref="ISecretProtector"/>. It envelopes secrets with PKCS#7/CMS:
/// the payload is encrypted with a fresh AES-256 content key, and that content key is wrapped for the
/// configured recipient certificate using RSA-OAEP (SHA-256). Protecting only needs the certificate's
/// public key (installer role); unprotecting needs its private key (service role). The produced payload
/// is self-describing CMS, so it can be read back on any platform that .NET supports.
/// </summary>
internal sealed class PkcsSecretProtector : ISecretProtector
{
    /// <summary>The stable protector name used to tag produced payloads.</summary>
    public const string ProtectorName = "pkcs";

    // AES-256-CBC content encryption. Set explicitly to avoid the weak 3DES default of EnvelopedCms.
    private static readonly Oid Aes256Cbc = new("2.16.840.1.101.3.4.1.42");

    private readonly X509Certificate2 _certificate;

    /// <summary>
    /// Creates a protector that envelopes secrets for the given <paramref name="certificate"/>. The
    /// certificate must expose a private key for <see cref="Unprotect"/> to succeed; a public-key-only
    /// certificate is sufficient for <see cref="Protect"/>.
    /// </summary>
    public PkcsSecretProtector(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        _certificate = certificate;
    }

    /// <inheritdoc />
    public string Name => ProtectorName;

    /// <inheritdoc />
    public byte[] Protect(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var envelope = new EnvelopedCms(new ContentInfo(plaintext), new AlgorithmIdentifier(Aes256Cbc));
        var recipient = new CmsRecipient(
            SubjectIdentifierType.IssuerAndSerialNumber, _certificate, RSAEncryptionPadding.OaepSHA256);
        envelope.Encrypt(recipient);
        return envelope.Encode();
    }

    /// <inheritdoc />
    public byte[] Unprotect(byte[] protectedData)
    {
        ArgumentNullException.ThrowIfNull(protectedData);

        var envelope = new EnvelopedCms();
        envelope.Decode(protectedData);
        envelope.Decrypt(new X509Certificate2Collection(_certificate));
        return envelope.ContentInfo.Content;
    }
}
