// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests.Authenticode;

using SAF.PluginSystem.Hosting.Extensions.Authenticode;
using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography.Pkcs;

public class AuthenticodeSignatureReaderTests
{
    private readonly AuthenticodeSignatureReader _reader = new();

    [Fact]
    public void ReadSignature_ReturnsNull_WhenFileIsNotSigned()
    {
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

            var tamperOffset = bytes.Length / 2;
            bytes[tamperOffset] ^= 0xFF;
            File.WriteAllBytes(tamperedPath, bytes);

            var result = _reader.ReadSignature(tamperedPath);

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

    [Fact]
    public void ReadSignature_VerifiesSnapshotsWithoutAFilePath()
    {
        var signedPath = FindSignedFile();
        Assert.SkipWhen(signedPath is null, "No Authenticode-signed binary available in this environment.");

        var verifier = new PathRecordingVerifier();
        var reader = new AuthenticodeSignatureReader(verifier);

        _ = reader.ReadSignature(File.ReadAllBytes(signedPath!));

        // Nothing is materialized on disk, so the verifier is called without a path.
        Assert.True(verifier.WasCalled);
        Assert.Null(verifier.ReceivedAssemblyPath);
    }

    [Fact]
    public void ReadSignature_FallsBackToChainTrust_WhenTheVerifierNeedsAFileButOnlyBytesAreAvailable()
    {
        var signedPath = FindSignedFile();
        Assert.SkipWhen(signedPath is null, "No Authenticode-signed binary available in this environment.");

        var verifier = new FileBoundVerifier();
        var reader = new AuthenticodeSignatureReader(verifier);

        _ = reader.ReadSignature(File.ReadAllBytes(signedPath!));

        // A file-bound verifier must never be handed a snapshot route it cannot serve without a temp file.
        Assert.False(verifier.WasCalled);
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

    private sealed class PathRecordingVerifier : IAuthenticodeChainTrustVerifier
    {
        public bool WasCalled { get; private set; }

        public string? ReceivedAssemblyPath { get; private set; }

        public bool VerifiesFileIntegrity => false;

        public bool RequiresFilePath => false;

        public bool IsTrusted(string? assemblyPath, SignedCms signedCms)
        {
            WasCalled = true;
            ReceivedAssemblyPath = assemblyPath;
            return false;
        }
    }

    private sealed class FileBoundVerifier : IAuthenticodeChainTrustVerifier
    {
        public bool WasCalled { get; private set; }

        public bool VerifiesFileIntegrity => true;

        public bool RequiresFilePath => true;

        public bool IsTrusted(string? assemblyPath, SignedCms signedCms)
        {
            WasCalled = true;
            return true;
        }
    }

    private sealed class ChainOnlyTrustingVerifier : IAuthenticodeChainTrustVerifier
    {
        public bool VerifiesFileIntegrity => false;

        public bool RequiresFilePath => false;

        public bool IsTrusted(string? assemblyPath, SignedCms signedCms) => true;
    }
}
