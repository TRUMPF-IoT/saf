// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Protection;

using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// Wraps another <see cref="ISecretProtector"/> and signs each payload it produces, so that writing a
/// value needs the signing certificate's private key. The inner payload is wrapped in a PKCS#7/CMS
/// <c>SignedData</c> signed with RSA-PSS and SHA-256, and the secret's name is part of what is signed. A
/// reader therefore refuses a forged value, a changed byte and a value moved to another name, none of
/// which <see cref="PkcsSecretProtector"/> alone can notice.
/// </summary>
/// <remarks>
/// <para>
/// A payload is verified against the configured certificate only; a payload that carries a certificate
/// of its own is refused. Neither the validity period nor a chain is checked, so a self-signed or
/// expired certificate works.
/// </para>
/// <para>
/// Not detected: a removed entry, an older value of the same name (rollback), and a value of the same
/// name copied from another store signed with the same certificate.
/// </para>
/// <para>
/// Signing needs the private key and a key usage that allows digital signatures. Verifying needs only
/// the public key; decrypting is up to the inner protector.
/// </para>
/// </remarks>
public sealed class SigningSecretProtector : ISecretProtector, IDisposable
{
    /// <summary>Appended to the inner protector's name to form this protector's <see cref="Name"/>.</summary>
    public const string NameSuffix = "-signed";

    private static readonly Oid Sha256 = new("2.16.840.1.101.3.4.2.1");
    private static readonly string DocumentNameOid = new Pkcs9DocumentName().Oid!.Value!;

    private readonly ISecretProtector _inner;
    private readonly X509Certificate2 _certificate;
    private bool _disposed;

    /// <summary>
    /// Creates a protector that signs the payloads of <paramref name="inner"/> with
    /// <paramref name="certificate"/>. The certificate needs an RSA key, and if it has a key usage
    /// extension, that must allow digital signatures. This protector takes ownership of both arguments and
    /// disposes them when it is disposed itself, so the certificate may be the same instance the inner
    /// protector was given.
    /// </summary>
    public SigningSecretProtector(ISecretProtector inner, X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(certificate);

        using (var publicKey = certificate.GetRSAPublicKey())
        {
            if (publicKey is null)
            {
                throw new ArgumentException(
                    $"The certificate '{certificate.Subject}' has no RSA key. Secrets are signed with RSA-PSS.",
                    nameof(certificate));
            }
        }

        if (certificate.Extensions.OfType<X509KeyUsageExtension>().SingleOrDefault() is { } keyUsage
            && !keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature))
        {
            throw new ArgumentException(
                $"The key usage of the certificate '{certificate.Subject}' ({keyUsage.KeyUsages}) does not allow " +
                "digital signatures. Use a certificate whose key usage includes digitalSignature.",
                nameof(certificate));
        }

        _inner = inner;
        _certificate = certificate;
    }

    /// <inheritdoc />
    public string Name => _inner.Name + NameSuffix;

    /// <summary>
    /// Protects <paramref name="plaintext"/> with the inner protector and signs the result together with
    /// <paramref name="name"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> contains NUL or an unpaired surrogate.</exception>
    /// <exception cref="InvalidOperationException">The certificate has no private key.</exception>
    /// <exception cref="CryptographicException">The key cannot sign.</exception>
    public byte[] Protect(string name, byte[] plaintext)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(plaintext);

        var nameAttribute = CreateNameAttribute(name);

        if (!_certificate.HasPrivateKey)
        {
            throw new InvalidOperationException(
                $"The certificate '{_certificate.Subject}' has no private key, so it cannot sign the secret " +
                $"'{name}'. Writing a signed secret needs the private key, also on a host that only provisions secrets.");
        }

        var signed = new SignedCms(new ContentInfo(_inner.Protect(name, plaintext)), detached: false);
        try
        {
            using var privateKey = _certificate.GetRSAPrivateKey()!;
            var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, _certificate, privateKey, RSASignaturePadding.Pss)
            {
                DigestAlgorithm = Sha256,
                IncludeOption = X509IncludeOption.None
            };
            signer.SignedAttributes.Add(nameAttribute);
            signed.ComputeSignature(signer, silent: true);
        }
        catch (CryptographicException e)
        {
            throw new CryptographicException(
                $"The secret '{name}' could not be signed with the certificate '{_certificate.Subject}' " +
                $"({_certificate.Thumbprint}). Check that this account may use its private key and that the key " +
                "may sign; a key restricted to decryption cannot.", e);
        }

        return signed.Encode();
    }

    /// <summary>
    /// Verifies that <paramref name="protectedData"/> was signed with this protector's certificate for
    /// <paramref name="name"/>, then recovers the plaintext with the inner protector.
    /// </summary>
    /// <exception cref="CryptographicException">
    /// The payload is not signed, is signed by another key or for another name, or was changed.
    /// </exception>
    public byte[] Unprotect(string name, byte[] protectedData)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(protectedData);

        var signed = Decode(name, protectedData);
        var signerInfo = GetOnlySigner(name, signed);
        VerifySignature(name, signerInfo);
        VerifyName(name, signerInfo);

        return _inner.Unprotect(name, signed.ContentInfo.Content);
    }

    /// <summary>Disposes the inner protector, if it is disposable, and the certificate.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        (_inner as IDisposable)?.Dispose();
        _certificate.Dispose();
    }

    // The attribute stores NUL-terminated UTF-16, so only a name that survives the round trip is bound exactly.
    private static Pkcs9DocumentName CreateNameAttribute(string name)
    {
        var attribute = new Pkcs9DocumentName(name);
        if (!string.Equals(new Pkcs9DocumentName(attribute.RawData!).DocumentName, name, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The name of a signed secret must not contain NUL or an unpaired surrogate.", nameof(name));
        }

        return attribute;
    }

    private static SignedCms Decode(string name, byte[] protectedData)
    {
        var signed = new SignedCms();
        try
        {
            signed.Decode(protectedData);
        }
        catch (CryptographicException e)
        {
            throw new CryptographicException(
                $"The value of the secret '{name}' is not signed. Only values written through " +
                $"{nameof(SigningSecretProtector)} are accepted; re-provision the secret.", e);
        }

        return signed;
    }

    private static SignerInfo GetOnlySigner(string name, SignedCms signed)
    {
        if (signed.SignerInfos.Count != 1)
        {
            throw new CryptographicException(
                $"The value of the secret '{name}' has {signed.SignerInfos.Count} signatures instead of exactly one.");
        }

        // CheckSignature would prefer a certificate carried in the message over the configured one.
        if (signed.Certificates.Count != 0)
        {
            throw new CryptographicException(
                $"The value of the secret '{name}' carries certificates. It is verified against the configured " +
                "certificate only, and a value written by this protector carries none.");
        }

        return signed.SignerInfos[0];
    }

    private void VerifySignature(string name, SignerInfo signerInfo)
    {
        var signer = signerInfo.SignerIdentifier;
        if (signer.Type != SubjectIdentifierType.IssuerAndSerialNumber || !signer.MatchesCertificate(_certificate))
        {
            throw new CryptographicException(
                $"The value of the secret '{name}' was not signed with the certificate '{_certificate.Subject}' " +
                $"({_certificate.Thumbprint}).");
        }

        try
        {
            // No chain: trust comes from the configured certificate, not from its issuer or its dates.
            signerInfo.CheckSignature(new X509Certificate2Collection(_certificate), verifySignatureOnly: true);
        }
        catch (CryptographicException e)
        {
            throw new CryptographicException(
                $"The signature of the secret '{name}' is not valid for the certificate '{_certificate.Subject}' " +
                $"({_certificate.Thumbprint}). The value was changed or signed with another key.", e);
        }
    }

    // Runs after the signature check, so the name it reads is one the certificate's key signed.
    private static void VerifyName(string name, SignerInfo signerInfo)
    {
        var signedName = ReadSignedName(name, signerInfo);

        // Case-insensitive like the store's keys.
        if (!string.Equals(signedName, name, StringComparison.OrdinalIgnoreCase))
        {
            throw new CryptographicException(
                $"The value stored as the secret '{name}' was signed for the secret '{signedName}'.");
        }
    }

    private static string ReadSignedName(string name, SignerInfo signerInfo)
    {
        var attributes = signerInfo.SignedAttributes
            .Cast<CryptographicAttributeObject>()
            .Where(a => a.Oid.Value == DocumentNameOid)
            .ToList();

        if (attributes is not [{ Values: [var value] }])
        {
            throw new CryptographicException(
                $"The value of the secret '{name}' does not name exactly one secret in its signed attributes.");
        }

        return new Pkcs9DocumentName(value.RawData!).DocumentName;
    }
}
