// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests.Authenticode;

using System.Buffers.Binary;
using System.Formats.Asn1;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Produces a genuinely Authenticode-signed PE image in memory, so that the accepted path through
/// <see cref="Extensions.Authenticode.AuthenticodeSignatureReader"/> can be asserted on any operating
/// system and without the signtool fixture, which only exists on Windows CI.
/// </summary>
/// <remarks>
/// The digest is computed here from the Authenticode exclusion rules written out a second time, rather
/// than by calling the hasher under test, so a positive result means two independent implementations
/// agree. What this cannot prove is agreement with Microsoft: the signtool fixture stays the oracle for
/// that, and it also covers signtool's exact encapsulation of the signed content, which differs from
/// what <see cref="SignedCms"/> emits.
/// </remarks>
internal static class AuthenticodeTestPeSigner
{
    // 1.3.6.1.4.1.311.2.1.4 - SPC_INDIRECT_DATA_OBJID, 1.3.6.1.4.1.311.2.1.15 - SPC_PE_IMAGE_DATA_OBJID.
    private const string SpcIndirectDataOid = "1.3.6.1.4.1.311.2.1.4";
    private const string SpcPeImageDataOid = "1.3.6.1.4.1.311.2.1.15";

    private const int OptionalHeaderCheckSumOffset = 64;
    private const int CheckSumSize = 4;
    private const int Pe32DataDirectoriesOffset = 96;
    private const int Pe32PlusDataDirectoriesOffset = 112;
    private const int DataDirectoryEntrySize = 8;
    private const int CertificateTableDirectoryIndex = 4;
    private const int WinCertificateHeaderSize = 8;
    private const ushort WinCertificateRevision200 = 0x0200;
    private const ushort WinCertificateTypePkcsSignedData = 0x0002;

    /// <summary>
    /// Signs an unsigned PE image and returns the signed copy.
    /// </summary>
    /// <param name="decorateSignature">
    /// Runs after the signature is computed, to attach counter-signatures or timestamp tokens.
    /// </param>
    public static byte[] Sign(
        byte[] unsignedImage,
        X509Certificate2 signer,
        X509Certificate2[] embeddedCertificates,
        HashAlgorithmName? hashAlgorithm = null,
        Action<SignedCms>? decorateSignature = null)
    {
        var algorithm = hashAlgorithm ?? HashAlgorithmName.SHA256;
        var (checkSumOffset, certificateTableEntryOffset) = LocatePeFields(unsignedImage);

        var digest = ComputeAuthenticodeDigest(unsignedImage, checkSumOffset, certificateTableEntryOffset, algorithm);
        var content = EncodeSpcIndirectDataContent(digest, algorithm);

        var signedCms = new SignedCms(new ContentInfo(new Oid(SpcIndirectDataOid), content), detached: false);
        signedCms.ComputeSignature(new CmsSigner(signer)
        {
            IncludeOption = X509IncludeOption.EndCertOnly,
            DigestAlgorithm = new Oid(DigestOid(algorithm))
        });

        foreach (var certificate in embeddedCertificates)
        {
            signedCms.AddCertificate(certificate);
        }

        decorateSignature?.Invoke(signedCms);

        return AppendCertificateTable(unsignedImage, certificateTableEntryOffset, signedCms.Encode());
    }

    /// <summary>Reads an unsigned PE image to sign. The image must not already carry a certificate table.</summary>
    public static byte[] ReadUnsignedImage(string assemblyPath)
    {
        var image = File.ReadAllBytes(assemblyPath);
        using var stream = new MemoryStream(image, writable: false);
        using var peReader = new PEReader(stream);
        var peHeader = peReader.PEHeaders.PEHeader ?? throw new InvalidOperationException("PE header is missing.");
        if (peHeader.CertificateTableDirectory.Size != 0)
        {
            throw new InvalidOperationException($"{assemblyPath} is already signed.");
        }

        return image;
    }

    private static (int CheckSumOffset, int CertificateTableEntryOffset) LocatePeFields(byte[] image)
    {
        using var stream = new MemoryStream(image, writable: false);
        using var peReader = new PEReader(stream);
        var peHeaders = peReader.PEHeaders;
        var peHeader = peHeaders.PEHeader ?? throw new InvalidOperationException("PE header is missing.");

        var dataDirectoriesOffset = peHeaders.PEHeaderStartOffset +
            (peHeader.Magic == PEMagic.PE32Plus ? Pe32PlusDataDirectoriesOffset : Pe32DataDirectoriesOffset);

        return (
            peHeaders.PEHeaderStartOffset + OptionalHeaderCheckSumOffset,
            dataDirectoriesOffset + (CertificateTableDirectoryIndex * DataDirectoryEntrySize));
    }

    // Authenticode hashes the image in ascending order, skipping the optional-header CheckSum and the
    // certificate-table data directory entry. The certificate table itself is appended afterwards and is
    // never hashed, so the digest of the unsigned image is the digest of the signed one.
    private static byte[] ComputeAuthenticodeDigest(
        byte[] image,
        int checkSumOffset,
        int certificateTableEntryOffset,
        HashAlgorithmName hashAlgorithm)
    {
        using var hasher = IncrementalHash.CreateHash(hashAlgorithm);
        hasher.AppendData(image, 0, checkSumOffset);
        hasher.AppendData(
            image,
            checkSumOffset + CheckSumSize,
            certificateTableEntryOffset - (checkSumOffset + CheckSumSize));
        hasher.AppendData(
            image,
            certificateTableEntryOffset + DataDirectoryEntrySize,
            image.Length - (certificateTableEntryOffset + DataDirectoryEntrySize));
        return hasher.GetHashAndReset();
    }

    private static byte[] EncodeSpcIndirectDataContent(byte[] digest, HashAlgorithmName hashAlgorithm)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            // SpcAttributeTypeAndOptionalValue. The reader skips it wholesale, so an empty
            // SpcPeImageData is enough to keep the structure well formed.
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(SpcPeImageDataOid);
                using (writer.PushSequence())
                {
                }
            }

            // DigestInfo
            using (writer.PushSequence())
            {
                using (writer.PushSequence())
                {
                    writer.WriteObjectIdentifier(DigestOid(hashAlgorithm));
                    writer.WriteNull();
                }

                writer.WriteOctetString(digest);
            }
        }

        return writer.Encode();
    }

    private static byte[] AppendCertificateTable(byte[] unsignedImage, int certificateTableEntryOffset, byte[] signature)
    {
        var entryLength = WinCertificateHeaderSize + signature.Length;
        var alignedLength = (entryLength + 7) & ~7;

        var signedImage = new byte[unsignedImage.Length + alignedLength];
        unsignedImage.CopyTo(signedImage, 0);

        var entry = signedImage.AsSpan(unsignedImage.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(entry, (uint)entryLength);
        BinaryPrimitives.WriteUInt16LittleEndian(entry[4..], WinCertificateRevision200);
        BinaryPrimitives.WriteUInt16LittleEndian(entry[6..], WinCertificateTypePkcsSignedData);
        signature.CopyTo(entry[WinCertificateHeaderSize..]);

        // For the certificate table the data directory holds a file offset, not an RVA.
        var directoryEntry = signedImage.AsSpan(certificateTableEntryOffset, DataDirectoryEntrySize);
        BinaryPrimitives.WriteInt32LittleEndian(directoryEntry, unsignedImage.Length);
        BinaryPrimitives.WriteInt32LittleEndian(directoryEntry[4..], alignedLength);

        return signedImage;
    }

    private static string DigestOid(HashAlgorithmName hashAlgorithm) => hashAlgorithm.Name switch
    {
        nameof(HashAlgorithmName.SHA256) => "2.16.840.1.101.3.4.2.1",
        nameof(HashAlgorithmName.SHA384) => "2.16.840.1.101.3.4.2.2",
        nameof(HashAlgorithmName.SHA512) => "2.16.840.1.101.3.4.2.3",
        _ => throw new ArgumentOutOfRangeException(nameof(hashAlgorithm), hashAlgorithm, "Unsupported digest algorithm.")
    };
}
