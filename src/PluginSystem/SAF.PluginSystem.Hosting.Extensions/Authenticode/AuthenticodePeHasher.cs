// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Authenticode;

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Formats.Asn1;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;

/// <summary>
/// Default <see cref="IAuthenticodePeHasher"/> implementation.
/// <para>
/// <see cref="SignedCms.CheckSignature(bool)"/> alone only proves that the signed
/// <c>SpcIndirectDataContent</c> blob is internally consistent - it does not prove that the blob
/// belongs to <em>this</em> file. Without recomputing the PE hash and comparing it to the digest
/// embedded in the signature, a valid signature blob could be transplanted from a trusted file onto
/// a tampered one. This type closes that gap and works identically on every platform.
/// </para>
/// </summary>
internal sealed class AuthenticodePeHasher : IAuthenticodePeHasher
{
    // 1.3.6.1.4.1.311.2.1.4 - SPC_INDIRECT_DATA_OBJID (Authenticode signed content).
    private const string SpcIndirectDataOid = "1.3.6.1.4.1.311.2.1.4";

    // Offset of the CheckSum field inside the PE optional header (identical for PE32 and PE32+).
    private const int OptionalHeaderCheckSumOffset = 64;
    private const int CheckSumSize = 4;

    // Offset of the data directory array inside the optional header.
    private const int Pe32DataDirectoriesOffset = 96;
    private const int Pe32PlusDataDirectoriesOffset = 112;

    // The certificate table is data directory entry #4; each entry is 8 bytes.
    private const int DataDirectoryEntrySize = 8;
    private const int CertificateTableDirectoryIndex = 4;

    private readonly IAuthenticodeCertificateTableParser _certificateTableParser;

    internal AuthenticodePeHasher()
        : this(new AuthenticodeCertificateTableParser())
    {
    }

    internal AuthenticodePeHasher(IAuthenticodeCertificateTableParser certificateTableParser)
    {
        ArgumentNullException.ThrowIfNull(certificateTableParser);
        _certificateTableParser = certificateTableParser;
    }

    public bool VerifyEmbeddedHashMatchesFile(string assemblyPath, SignedCms signedCms)
    {
        if (!TryReadExpectedDigest(signedCms, out var hashAlgorithm, out var expectedDigest))
        {
            return false;
        }

        var actualDigest = ComputeAuthenticodeHash(assemblyPath, hashAlgorithm);
        return actualDigest is not null && CryptographicOperations.FixedTimeEquals(actualDigest, expectedDigest);
    }

    public bool VerifyEmbeddedHashMatchesFile(ReadOnlyMemory<byte> assemblyBytes, SignedCms signedCms)
    {
        if (!TryReadExpectedDigest(signedCms, out var hashAlgorithm, out var expectedDigest))
        {
            return false;
        }

        var actualDigest = ComputeAuthenticodeHash(assemblyBytes, hashAlgorithm);
        return actualDigest is not null && CryptographicOperations.FixedTimeEquals(actualDigest, expectedDigest);
    }

    [SuppressMessage("Code Smell", "S125:Sections of code should not be commented out", Justification = "The comment documents the ASN.1 binary structure and is not commented-out executable code.")]
    private static bool TryReadExpectedDigest(SignedCms signedCms, out HashAlgorithmName hashAlgorithm, out byte[] expectedDigest)
    {
        hashAlgorithm = default;
        expectedDigest = [];

        if (signedCms.ContentInfo.ContentType.Value != SpcIndirectDataOid)
        {
            return false;
        }

        try
        {
            // SpcIndirectDataContent ::= SEQUENCE {
            //     data          SpcAttributeTypeAndOptionalValue,
            //     messageDigest DigestInfo }
            // DigestInfo ::= SEQUENCE { digestAlgorithm AlgorithmIdentifier, digest OCTET STRING }
            var content = new AsnReader(signedCms.ContentInfo.Content, AsnEncodingRules.BER);
            var spcIndirectDataContent = content.ReadSequence();

            _ = spcIndirectDataContent.ReadEncodedValue(); // skip SpcAttributeTypeAndOptionalValue

            var digestInfo = spcIndirectDataContent.ReadSequence();
            var algorithmIdentifier = digestInfo.ReadSequence();
            var digestOid = algorithmIdentifier.ReadObjectIdentifier();
            expectedDigest = digestInfo.ReadOctetString();

            var mapped = MapDigestOid(digestOid);
            if (mapped is null)
            {
                return false;
            }

            hashAlgorithm = mapped.Value;
            return true;
        }
        catch (AsnContentException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private byte[]? ComputeAuthenticodeHash(string assemblyPath, HashAlgorithmName hashAlgorithm)
    {
        using var stream = File.OpenRead(assemblyPath);
        return ComputeAuthenticodeHash(stream, hashAlgorithm);
    }

    private byte[]? ComputeAuthenticodeHash(ReadOnlyMemory<byte> assemblyBytes, HashAlgorithmName hashAlgorithm)
    {
        using var stream = new MemoryStream(assemblyBytes.ToArray(), writable: false);
        return ComputeAuthenticodeHash(stream, hashAlgorithm);
    }

    private byte[]? ComputeAuthenticodeHash(Stream stream, HashAlgorithmName hashAlgorithm)
    {
        using var peReader = new PEReader(stream);

        var peHeaders = peReader.PEHeaders;
        var peHeader = peHeaders.PEHeader;
        if (peHeader is null)
        {
            return null;
        }

        var peHeaderStart = peHeaders.PEHeaderStartOffset;
        var checkSumStart = peHeaderStart + OptionalHeaderCheckSumOffset;

        var dataDirectoriesStart = peHeaderStart +
            (peHeader.Magic == PEMagic.PE32Plus ? Pe32PlusDataDirectoriesOffset : Pe32DataDirectoriesOffset);
        var certificateTableEntryStart = dataDirectoriesStart + (CertificateTableDirectoryIndex * DataDirectoryEntrySize);

        // For the certificate table the data directory holds a file offset, not an RVA.
        var certificateTableStart = (long)peHeader.CertificateTableDirectory.RelativeVirtualAddress;
        var certificateTableSize = (long)peHeader.CertificateTableDirectory.Size;
        var fileLength = stream.Length;
        if (certificateTableStart <= 0 ||
            certificateTableSize <= 0 ||
            certificateTableStart > fileLength ||
            certificateTableSize > fileLength - certificateTableStart ||
            !_certificateTableParser.IsWellFormed(stream, certificateTableStart, certificateTableSize))
        {
            return null;
        }

        // Authenticode omits three ranges from the hash: the optional-header CheckSum field, the
        // certificate-table data directory entry, and the certificate table itself. Everything else
        // is hashed in ascending file order.
        var endBeforeCertificateTable = certificateTableStart > 0 ? certificateTableStart : fileLength;

        using var hasher = IncrementalHash.CreateHash(hashAlgorithm);

        HashRange(stream, hasher, 0, checkSumStart);
        HashRange(stream, hasher, checkSumStart + CheckSumSize, certificateTableEntryStart);
        HashRange(stream, hasher, certificateTableEntryStart + DataDirectoryEntrySize, endBeforeCertificateTable);

        if (certificateTableStart > 0)
        {
            var afterCertificateTable = certificateTableStart + certificateTableSize;
            if (afterCertificateTable < fileLength)
            {
                HashRange(stream, hasher, afterCertificateTable, fileLength);
            }
        }

        return hasher.GetHashAndReset();
    }

    private static void HashRange(Stream stream, IncrementalHash hasher, long start, long end)
    {
        if (end <= start)
        {
            return;
        }

        stream.Position = start;
        var remaining = end - start;
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            while (remaining > 0)
            {
                var toRead = (int)Math.Min(buffer.Length, remaining);
                stream.ReadExactly(buffer, 0, toRead);
                hasher.AppendData(buffer, 0, toRead);
                remaining -= toRead;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static HashAlgorithmName? MapDigestOid(string digestOid) => digestOid switch
    {
        "1.3.14.3.2.26" => HashAlgorithmName.SHA1,
        "2.16.840.1.101.3.4.2.1" => HashAlgorithmName.SHA256,
        "2.16.840.1.101.3.4.2.2" => HashAlgorithmName.SHA384,
        "2.16.840.1.101.3.4.2.3" => HashAlgorithmName.SHA512,
        _ => null
    };
}
