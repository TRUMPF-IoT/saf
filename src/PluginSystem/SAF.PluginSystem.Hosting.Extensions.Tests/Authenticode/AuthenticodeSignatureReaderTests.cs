// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests.Authenticode;

using SAF.PluginSystem.Hosting.Extensions.Authenticode;
using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography.X509Certificates;

public class AuthenticodeSignatureReaderTests
{
    private readonly AuthenticodeSignatureReader _reader = new();

    [Fact]
    public void ReadSignature_ReturnsNull_WhenFileIsNotSigned()
    {
        // The test assembly itself is not Authenticode signed.
        var unsignedPath = typeof(AuthenticodeSignatureReaderTests).Assembly.Location;

        var result = _reader.ReadSignature(unsignedPath);

        Assert.Null(result);
    }

    [Fact]
    public void ReadSignature_ReturnsNull_WhenContentSnapshotIsNotSigned()
    {
        var unsignedBytes = File.ReadAllBytes(typeof(AuthenticodeSignatureReaderTests).Assembly.Location);

        var result = _reader.ReadSignature(unsignedBytes);

        Assert.Null(result);
    }

    [Fact]
    public void ReadSignature_ReturnsNull_WhenFileDoesNotExist()
    {
        var result = _reader.ReadSignature("does-not-exist.dll");

        Assert.Null(result);
    }

    [Fact]
    public void ReadSignature_ReturnsNull_WhenCertificateTableSizeExceedsRemainingFile()
    {
        var path = CreatePeWithCertificateTable(int.MaxValue, []);
        try
        {
            var result = _reader.ReadSignature(path);

            Assert.Null(result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadSignature_ReturnsNull_WhenWinCertificateLengthOverflowsBoundsCheck()
    {
        var certificateBlob = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(certificateBlob.AsSpan(0, 4), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(certificateBlob.AsSpan(6, 2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(certificateBlob.AsSpan(8, 4), 0x7FFF_FFFFu);
        BinaryPrimitives.WriteUInt16LittleEndian(certificateBlob.AsSpan(14, 2), 2);

        var path = CreatePeWithCertificateTable(certificateBlob.Length, certificateBlob);
        try
        {
            var result = _reader.ReadSignature(path);

            Assert.Null(result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadSignature_ReportsSignerThumbprint_ForSignedFile()
    {
        var signedPath = FindSignedFile();
        Assert.SkipWhen(signedPath is null, "No Authenticode-signed binary available in this environment.");

        var result = _reader.ReadSignature(signedPath!);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result!.SignerThumbprint));
    }

    [Fact]
    public void ReadSignature_RejectsSignerThumbprint_WhenFileIsTampered()
    {
        var signedPath = FindSignedFile();
        Assert.SkipWhen(signedPath is null, "No Authenticode-signed binary available in this environment.");

        var tamperedPath = Path.Combine(Path.GetTempPath(), $"tampered-{Guid.NewGuid():N}.dll");
        try
        {
            var bytes = File.ReadAllBytes(signedPath!);

            // Flip a byte roughly in the middle of the file - inside the hashed section content but
            // before the trailing certificate table - so the embedded digest no longer matches.
            var tamperOffset = bytes.Length / 2;
            bytes[tamperOffset] ^= 0xFF;
            File.WriteAllBytes(tamperedPath, bytes);

            var result = _reader.ReadSignature(tamperedPath);

            // The signature no longer covers the file: no trustworthy signer, not a valid signature.
            Assert.Null(result?.SignerThumbprint);
            Assert.False(result?.HasValidDigitalSignature ?? false);
        }
        finally
        {
            if (File.Exists(tamperedPath))
            {
                File.Delete(tamperedPath);
            }
        }
    }

    [Fact]
    public void ReadSignature_RejectsTamperedFile_EvenWhenChainOnlyVerifierTrustsTheSigner()
    {
        // Simulates the non-Windows path: the trust verifier validates only the certificate chain and
        // does NOT confirm the signature covers the file. Without the mandatory PE-hash comparison a
        // transplanted/tampered signature would slip through, so this guards that regression.
        var signedPath = FindSignedFile();
        Assert.SkipWhen(signedPath is null, "No Authenticode-signed binary available in this environment.");

        var tamperedPath = Path.Combine(Path.GetTempPath(), $"tampered-{Guid.NewGuid():N}.dll");
        try
        {
            var bytes = File.ReadAllBytes(signedPath!);
            bytes[bytes.Length / 2] ^= 0xFF;
            File.WriteAllBytes(tamperedPath, bytes);

            var reader = new AuthenticodeSignatureReader(new ChainOnlyTrustingVerifier());

            var result = reader.ReadSignature(tamperedPath);

            Assert.Null(result?.SignerThumbprint);
            Assert.False(result?.HasValidDigitalSignature ?? false);
        }
        finally
        {
            if (File.Exists(tamperedPath))
            {
                File.Delete(tamperedPath);
            }
        }
    }

    private string? FindSignedFile()
    {
        foreach (var directory in CandidateDirectories())
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                continue;
            }

            foreach (var candidate in Directory.EnumerateFiles(directory, "*.dll"))
            {
                if (_reader.ReadSignature(candidate) is { SignerThumbprint: { Length: > 0 } })
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string?> CandidateDirectories()
    {
        yield return Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (OperatingSystem.IsWindows())
        {
            yield return Environment.SystemDirectory;
        }
    }

    private static string CreatePeWithCertificateTable(int certificateTableSize, byte[] certificateBlob)
    {
        var sourceBytes = File.ReadAllBytes(typeof(AuthenticodeSignatureReaderTests).Assembly.Location);
        var directoryOffset = FindCertificateTableDirectoryOffset(sourceBytes);
        var tableOffset = sourceBytes.Length;
        var peBytes = new byte[checked(sourceBytes.Length + certificateBlob.Length)];
        sourceBytes.CopyTo(peBytes, 0);
        certificateBlob.AsSpan().CopyTo(peBytes.AsSpan(tableOffset));

        BinaryPrimitives.WriteInt32LittleEndian(peBytes.AsSpan(directoryOffset, sizeof(int)), tableOffset);
        BinaryPrimitives.WriteInt32LittleEndian(
            peBytes.AsSpan(directoryOffset + sizeof(int), sizeof(int)),
            certificateTableSize);

        var path = Path.Combine(Path.GetTempPath(), $"authenticode-malformed-{Guid.NewGuid():N}.dll");
        File.WriteAllBytes(path, peBytes);
        return path;
    }

    private static int FindCertificateTableDirectoryOffset(byte[] peBytes)
    {
        using var stream = new MemoryStream(peBytes, writable: false);
        using var peReader = new PEReader(stream);
        var peHeaders = peReader.PEHeaders;
        var peHeader = peHeaders.PEHeader ?? throw new InvalidOperationException("PE header is missing.");
        var dataDirectoriesOffset = peHeaders.PEHeaderStartOffset +
            (peHeader.Magic == PEMagic.PE32Plus ? 112 : 96);

        return dataDirectoriesOffset + (4 * 8);
    }

    private sealed class ChainOnlyTrustingVerifier : IAuthenticodeChainTrustVerifier
    {
        public bool VerifiesFileIntegrity => false;

        public bool IsTrusted(string assemblyPath, X509Certificate2 signerCertificate) => true;
    }
}
