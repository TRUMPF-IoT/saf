// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests;

using SAF.PluginSystem.Hosting.Extensions;
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
    public void ReadSignature_ReturnsNull_WhenFileDoesNotExist()
    {
        var result = _reader.ReadSignature("does-not-exist.dll");

        Assert.Null(result);
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

    private sealed class ChainOnlyTrustingVerifier : IAuthenticodeChainTrustVerifier
    {
        public bool VerifiesFileIntegrity => false;

        public bool IsTrusted(string assemblyPath, X509Certificate2 signerCertificate) => true;
    }
}
