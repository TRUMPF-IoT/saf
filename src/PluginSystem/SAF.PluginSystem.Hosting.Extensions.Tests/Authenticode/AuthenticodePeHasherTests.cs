// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests.Authenticode;

using SAF.PluginSystem.Hosting.Extensions.Authenticode;
using System.Buffers.Binary;
using System.Formats.Asn1;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;

public sealed class AuthenticodePeHasherTests
{
    [Fact]
    public void VerifyEmbeddedHashMatchesFile_ReturnsFalse_WhenContentTypeIsNotAuthenticode()
    {
        var hasher = new AuthenticodePeHasher(new AuthenticodeCertificateTableParser());
        var signedCms = new SignedCms(new ContentInfo([]));

        Assert.False(hasher.VerifyEmbeddedHashMatchesFile("unused.dll", signedCms));
    }

    [Fact]
    public void VerifyEmbeddedHashMatchesFile_ReturnsFalse_WhenDigestAlgorithmIsUnsupported()
    {
        var hasher = new AuthenticodePeHasher(new AuthenticodeCertificateTableParser());
        var signedCms = CreateSignedCms("1.2.3.4", [0x01]);

        Assert.False(hasher.VerifyEmbeddedHashMatchesFile("unused.dll", signedCms));
    }

    [Fact]
    public void VerifyEmbeddedHashMatchesFile_ReturnsFalse_WhenContentIsMalformed()
    {
        var contentInfo = new ContentInfo(
            new Oid("1.3.6.1.4.1.311.2.1.4"),
            [0x01]);
        var signedCms = new SignedCms(contentInfo);
        var hasher = new AuthenticodePeHasher(new AuthenticodeCertificateTableParser());

        Assert.False(hasher.VerifyEmbeddedHashMatchesFile("unused.dll", signedCms));
    }

    [Fact]
    public void VerifyEmbeddedHashMatchesFile_ReturnsFalse_WhenPeHashDoesNotMatchEmbeddedDigest()
    {
        var assemblyPath = CreatePeWithCertificateTable();
        try
        {
            var signedCms = CreateSignedCms(
                "2.16.840.1.101.3.4.2.1",
                new byte[32]);
            var hasher = new AuthenticodePeHasher(new AuthenticodeCertificateTableParser());

            Assert.False(hasher.VerifyEmbeddedHashMatchesFile(assemblyPath, signedCms));
            Assert.False(hasher.VerifyEmbeddedHashMatchesFile(File.ReadAllBytes(assemblyPath), signedCms));
        }
        finally
        {
            File.Delete(assemblyPath);
        }
    }

    private static SignedCms CreateSignedCms(string digestOid, byte[] digest)
    {
        var writer = new AsnWriter(AsnEncodingRules.BER);
        writer.PushSequence();
        writer.PushSequence();
        writer.PopSequence();
        writer.PushSequence();
        writer.PushSequence();
        writer.WriteObjectIdentifier(digestOid);
        writer.PopSequence();
        writer.WriteOctetString(digest);
        writer.PopSequence();
        writer.PopSequence();

        var contentInfo = new ContentInfo(
            new Oid("1.3.6.1.4.1.311.2.1.4"),
            writer.Encode());
        return new SignedCms(contentInfo);
    }

    private static string CreatePeWithCertificateTable()
    {
        var sourceBytes = File.ReadAllBytes(typeof(AuthenticodePeHasherTests).Assembly.Location);
        var certificateBlob = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(certificateBlob, 8);

        using var sourceStream = new MemoryStream(sourceBytes, writable: false);
        using var peReader = new PEReader(sourceStream);
        var peHeaders = peReader.PEHeaders;
        var peHeader = peHeaders.PEHeader ?? throw new InvalidOperationException("PE header is missing.");
        var dataDirectoriesOffset = peHeaders.PEHeaderStartOffset +
            (peHeader.Magic == PEMagic.PE32Plus ? 112 : 96);
        var directoryOffset = dataDirectoriesOffset + (4 * 8);
        var certificateTableOffset = sourceBytes.Length;
        var peBytes = new byte[checked(sourceBytes.Length + certificateBlob.Length)];
        sourceBytes.CopyTo(peBytes, 0);
        certificateBlob.AsSpan().CopyTo(peBytes.AsSpan(certificateTableOffset));

        BinaryPrimitives.WriteInt32LittleEndian(peBytes.AsSpan(directoryOffset, sizeof(int)), certificateTableOffset);
        BinaryPrimitives.WriteInt32LittleEndian(
            peBytes.AsSpan(directoryOffset + sizeof(int), sizeof(int)),
            certificateBlob.Length);

        var path = Path.Combine(Path.GetTempPath(), $"authenticode-hasher-{Guid.NewGuid():N}.dll");
        File.WriteAllBytes(path, peBytes);
        return path;
    }
}