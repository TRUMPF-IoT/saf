// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Protection;

using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// Wraps another <see cref="ISecretProtector"/> and signs each payload it produces, so that the store
/// file is no longer trusted to its writers: a value can only be written by whoever holds the signing
/// certificate's private key. The inner payload is wrapped in a PKCS#7/CMS <c>SignedData</c> signed with
/// RSA-PSS and SHA-256, and the secret's name is part of what is signed. A reader therefore refuses a
/// forged value, a changed byte and a value moved to another name, all of which
/// <see cref="PkcsSecretProtector"/> alone cannot notice because protecting with it needs only the
/// public key.
/// </summary>
/// <remarks>
/// <para>
/// A payload is verified against the configured certificate only, never against a certificate the
/// payload carries itself; a payload that carries one is refused. Neither the certificate's validity
/// period nor a chain is checked: trust comes from the certificate being the one this host was
/// configured with, so a self-signed or expired certificate works.
/// </para>
/// <para>
/// Not detected: a removed entry, and an older value of the same name that was valid when it was
/// written (rollback).
/// </para>
/// <para>
/// Signing needs the private key. A host that only provisions secrets therefore needs it too, unlike
/// with <see cref="PkcsSecretProtector"/> alone. Verifying needs only the public key; decrypting is up
/// to the inner protector.
/// </para>
/// </remarks>
public sealed class SigningSecretProtector : ISecretProtector, IDisposable
{
    /// <summary>Appended to the inner protector's name to form this protector's <see cref="Name"/>.</summary>
    public const string NameSuffix = "+signed";

    private static readonly Oid Sha256 = new("2.16.840.1.101.3.4.2.1");

    // The signed attribute that carries the secret's name.
    private static readonly string DocumentNameOid = new Pkcs9DocumentName().Oid!.Value!;

    private readonly ISecretProtector _inner;
    private readonly X509Certificate2 _certificate;
    private bool _disposed;

    /// <summary>
    /// Creates a protector that signs the payloads of <paramref name="inner"/> with
    /// <paramref name="certificate"/>, which must have an RSA key. This protector takes ownership of both
    /// and disposes them when it is disposed itself, so the certificate may be the same instance the inner
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

        _inner = inner;
        _certificate = certificate;
    }

    /// <inheritdoc />
    public string Name => _inner.Name + NameSuffix;

    /// <summary>
    /// Not supported: a value is bound to its name. Call <see cref="Protect(string, byte[])"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public byte[] Protect(byte[] plaintext) => throw NameRequired();

    /// <summary>
    /// Not supported: a value is bound to its name. Call <see cref="Unprotect(string, byte[])"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public byte[] Unprotect(byte[] protectedData) => throw NameRequired();

    /// <summary>
    /// Protects <paramref name="plaintext"/> with the inner protector and signs the result together with
    /// <paramref name="name"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The certificate has no private key.</exception>
    public byte[] Protect(string name, byte[] plaintext)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(plaintext);

        if (!_certificate.HasPrivateKey)
        {
            throw new InvalidOperationException(
                $"The certificate '{_certificate.Subject}' has no private key, so it cannot sign the secret " +
                $"'{name}'. Writing a signed secret needs the private key, also on a host that only provisions secrets.");
        }

        var content = _inner.Protect(name, plaintext);

        using var privateKey = _certificate.GetRSAPrivateKey()!;
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, _certificate, privateKey, RSASignaturePadding.Pss)
        {
            DigestAlgorithm = Sha256,
            // The reader verifies against its own certificate only, so there is nothing to carry along.
            IncludeOption = X509IncludeOption.None
        };
        signer.SignedAttributes.Add(new Pkcs9DocumentName(name));

        var signed = new SignedCms(new ContentInfo(content), detached: false);
        signed.ComputeSignature(signer, silent: true);
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

    private static NotSupportedException NameRequired()
        => new($"{nameof(SigningSecretProtector)} binds each value to the name of its secret and needs that " +
            "name. Call the overload that takes it; a decorator around this protector must forward that overload.");

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

        if (signed.Detached)
        {
            throw new CryptographicException($"The value of the secret '{name}' does not contain the signed content.");
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

        // SignerInfo.CheckSignature prefers a certificate the message carries over the ones it is given,
        // so a message that carries any would let its author choose the key it is verified against.
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
        if (!signerInfo.SignerIdentifier.MatchesCertificate(_certificate))
        {
            throw new CryptographicException(
                $"The value of the secret '{name}' was not signed with the certificate '{_certificate.Subject}' " +
                $"({_certificate.Thumbprint}).");
        }

        try
        {
            // verifySignatureOnly: no chain is built. A self-signed certificate never chains to a trusted root,
            // and trust comes from the certificate being the configured one, not from its issuer or its dates.
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

        // Case-insensitive like the store's keys: names that differ only in case are the same entry.
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

        return new Pkcs9DocumentName(value.RawData).DocumentName;
    }
}
