// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests.Authenticode;

using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Attaches the two kinds of timestamp an Authenticode signature can carry: the legacy PKCS#9
/// counter-signature, and an RFC 3161 timestamp token.
/// </summary>
/// <remarks>
/// The BCL can request an RFC 3161 token but cannot issue one, so the token is assembled here: a TSTInfo
/// signed by a time-stamping certificate, complete with the ESS signing-certificate attribute that
/// <see cref="Rfc3161TimestampToken.TryDecode"/> expects. Without this there is no way to cover the
/// RFC 3161 branch offline, and it is the branch a real signtool signature actually uses.
/// </remarks>
internal static class AuthenticodeTestTimestamp
{
    // 1.2.840.113549.1.9.16.1.4 - id-ct-TSTInfo, 1.2.840.113549.1.9.16.2.14 - id-aa-timeStampToken,
    // 1.2.840.113549.1.9.16.2.47 - id-aa-signingCertificateV2.
    private const string TstInfoOid = "1.2.840.113549.1.9.16.1.4";
    private const string TimestampTokenOid = "1.2.840.113549.1.9.16.2.14";
    private const string SigningCertificateV2Oid = "1.2.840.113549.1.9.16.2.47";
    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";

    // Any OID identifies the TSA policy; nothing validates it.
    private const string TestPolicyOid = "1.3.6.1.4.1.311.2.1.4";

    public static void AddCounterSignature(SignedCms signedCms, X509Certificate2 signer, DateTimeOffset signingTime)
    {
        var counterSigner = new CmsSigner(signer) { IncludeOption = X509IncludeOption.EndCertOnly };
        counterSigner.SignedAttributes.Add(new Pkcs9SigningTime(signingTime.UtcDateTime));
        signedCms.SignerInfos[0].ComputeCounterSignature(counterSigner);
    }

    public static void AddRfc3161Timestamp(
        SignedCms signedCms,
        X509Certificate2 timestampAuthority,
        DateTimeOffset timestamp)
    {
        var signerInfo = signedCms.SignerInfos[0];
        var token = CreateToken(signerInfo.GetSignature(), timestampAuthority, timestamp);
        signerInfo.AddUnsignedAttribute(new AsnEncodedData(new Oid(TimestampTokenOid), token));
    }

    private static byte[] CreateToken(byte[] signatureValue, X509Certificate2 timestampAuthority, DateTimeOffset timestamp)
    {
        var tstInfo = EncodeTstInfo(SHA256.HashData(signatureValue), timestamp);

        var token = new SignedCms(new ContentInfo(new Oid(TstInfoOid), tstInfo), detached: false);
        var signer = new CmsSigner(timestampAuthority) { IncludeOption = X509IncludeOption.EndCertOnly };
        signer.SignedAttributes.Add(new AsnEncodedData(
            new Oid(SigningCertificateV2Oid),
            EncodeSigningCertificateV2(timestampAuthority)));
        token.ComputeSignature(signer);

        return token.Encode();
    }

    private static byte[] EncodeTstInfo(byte[] messageImprint, DateTimeOffset timestamp)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(1);
            writer.WriteObjectIdentifier(TestPolicyOid);

            // MessageImprint ::= SEQUENCE { hashAlgorithm AlgorithmIdentifier, hashedMessage OCTET STRING }
            using (writer.PushSequence())
            {
                using (writer.PushSequence())
                {
                    writer.WriteObjectIdentifier(Sha256Oid);
                    writer.WriteNull();
                }

                writer.WriteOctetString(messageImprint);
            }

            writer.WriteInteger(1);
            writer.WriteGeneralizedTime(timestamp.ToUniversalTime(), omitFractionalSeconds: true);
        }

        return writer.Encode();
    }

    // ESSCertIDv2 defaults the hash algorithm to SHA-256, so only the certificate hash is written.
    private static byte[] EncodeSigningCertificateV2(X509Certificate2 certificate)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            using (writer.PushSequence())
            {
                using (writer.PushSequence())
                {
                    writer.WriteOctetString(SHA256.HashData(certificate.RawData));
                }
            }
        }

        return writer.Encode();
    }
}
