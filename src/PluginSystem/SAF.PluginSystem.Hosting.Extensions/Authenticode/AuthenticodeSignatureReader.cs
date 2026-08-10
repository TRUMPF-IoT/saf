// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Authenticode;

using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

internal sealed class AuthenticodeSignatureReader : IAuthenticodeSignatureReader
{
    private const int WinCertificateHeaderSize = 8;

    private readonly IAuthenticodeChainTrustVerifier _trustVerifier;
    private readonly IAuthenticodePeHasher _peHasher;
    private readonly IAuthenticodeCertificateTableParser _certificateTableParser;

    public AuthenticodeSignatureReader()
        : this(
            OperatingSystem.IsWindows()
                ? new WindowsAuthenticodeTrustVerifier()
                : new CrossPlatformAuthenticodeTrustVerifier(),
            new AuthenticodeCertificateTableParser())
    {
    }

    internal AuthenticodeSignatureReader(IAuthenticodeChainTrustVerifier trustVerifier)
        : this(trustVerifier, new AuthenticodeCertificateTableParser())
    {
    }

    internal AuthenticodeSignatureReader(
        IAuthenticodeChainTrustVerifier trustVerifier,
        IAuthenticodeCertificateTableParser certificateTableParser)
        : this(trustVerifier, new AuthenticodePeHasher(certificateTableParser), certificateTableParser)
    {
    }

    internal AuthenticodeSignatureReader(
        IAuthenticodeChainTrustVerifier trustVerifier,
        IAuthenticodePeHasher peHasher)
        : this(trustVerifier, peHasher, new AuthenticodeCertificateTableParser())
    {
    }

    internal AuthenticodeSignatureReader(
        IAuthenticodeChainTrustVerifier trustVerifier,
        IAuthenticodePeHasher peHasher,
        IAuthenticodeCertificateTableParser certificateTableParser)
    {
        ArgumentNullException.ThrowIfNull(trustVerifier);
        ArgumentNullException.ThrowIfNull(peHasher);
        ArgumentNullException.ThrowIfNull(certificateTableParser);
        _trustVerifier = trustVerifier;
        _peHasher = peHasher;
        _certificateTableParser = certificateTableParser;
    }

    /// <inheritdoc />
    public AuthenticodeSignatureInfo? ReadSignature(string assemblyPath)
    {
        try
        {
            return ReadSignatureCore(
                TryReadAuthenticodeSignedData(assemblyPath),
                signerCertificate => _trustVerifier.IsTrusted(assemblyPath, signerCertificate),
                cms => _peHasher.VerifyEmbeddedHashMatchesFile(assemblyPath, cms));
        }
        catch (Exception ex) when (ex is CryptographicException
                                   or PlatformNotSupportedException
                                   or FileNotFoundException
                                   or DirectoryNotFoundException
                                   or IOException
                                   or BadImageFormatException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public AuthenticodeSignatureInfo? ReadSignature(ReadOnlyMemory<byte> assemblyBytes)
    {
        try
        {
            var signedData = TryReadAuthenticodeSignedData(assemblyBytes);
            if (signedData is null)
            {
                return null;
            }

            var trustSnapshotPath = Path.Combine(Path.GetTempPath(), $"saf-authenticode-{Guid.NewGuid():N}.dll");
            try
            {
                using var trustSnapshot = OpenTrustSnapshot(assemblyBytes, trustSnapshotPath);
                return ReadSignatureCore(
                    signedData,
                    signerCertificate => _trustVerifier.IsTrusted(trustSnapshot.Name, signerCertificate),
                    cms => _peHasher.VerifyEmbeddedHashMatchesFile(assemblyBytes, cms));
            }
            finally
            {
                File.Delete(trustSnapshotPath);
            }
        }
        catch (Exception ex) when (ex is CryptographicException
                                   or PlatformNotSupportedException
                                   or FileNotFoundException
                                   or DirectoryNotFoundException
                                   or IOException
                                   or BadImageFormatException)
        {
            return null;
        }
    }

    private AuthenticodeSignatureInfo? ReadSignatureCore(
        byte[]? signedData,
        Func<X509Certificate2, bool> trustVerifier,
        Func<SignedCms, bool> fileIntegrityVerifier)
    {
        if (signedData is null)
        {
            return null;
        }

        var cms = new SignedCms();
        cms.Decode(signedData);
        cms.CheckSignature(verifySignatureOnly: true);

        using var signerCertificate = ResolveSignerCertificate(cms);
        if (signerCertificate is null)
        {
            return new AuthenticodeSignatureInfo(SignerThumbprint: null, HasValidDigitalSignature: false);
        }

        var isTrusted = trustVerifier(signerCertificate);

        var signatureCoversFile = (isTrusted && _trustVerifier.VerifiesFileIntegrity)
            || fileIntegrityVerifier(cms);

        var signerThumbprint = signatureCoversFile ? NormalizeThumbprint(signerCertificate.Thumbprint) : null;
        return new AuthenticodeSignatureInfo(signerThumbprint, isTrusted && signatureCoversFile);
    }

    private static X509Certificate2? ResolveSignerCertificate(SignedCms cms)
    {
        var signerInfos = cms.SignerInfos;
        if (signerInfos.Count == 0)
        {
            return null;
        }

        return signerInfos[0].Certificate;
    }

    private static string? NormalizeThumbprint(string? thumbprint)
        => string.IsNullOrWhiteSpace(thumbprint)
            ? null
            : thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal);

    private byte[]? TryReadAuthenticodeSignedData(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        return TryReadAuthenticodeSignedData(stream);
    }

    private byte[]? TryReadAuthenticodeSignedData(ReadOnlyMemory<byte> assemblyBytes)
    {
        using var stream = new MemoryStream(assemblyBytes.ToArray(), writable: false);
        return TryReadAuthenticodeSignedData(stream);
    }

    private byte[]? TryReadAuthenticodeSignedData(Stream stream)
    {
        using var peReader = new PEReader(stream);
        var certificateDirectory = peReader.PEHeaders.PEHeader?.CertificateTableDirectory;

        if (certificateDirectory is null ||
            certificateDirectory.Value.RelativeVirtualAddress <= 0 ||
            certificateDirectory.Value.Size <= WinCertificateHeaderSize)
        {
            return null;
        }

        var certificateTableOffset = (long)certificateDirectory.Value.RelativeVirtualAddress;
        var certificateTableSize = (long)certificateDirectory.Value.Size;
        var fileLength = stream.Length;
        if (certificateTableOffset > fileLength ||
            certificateTableSize > fileLength - certificateTableOffset)
        {
            return null;
        }

        // For the certificate table the data directory holds a file offset, not an RVA.
        stream.Position = certificateTableOffset;
        var certificateBlob = new byte[(int)certificateTableSize];
        try
        {
            stream.ReadExactly(certificateBlob);
        }
        catch (EndOfStreamException)
        {
            return null;
        }

        return _certificateTableParser.TryExtractPkcsSignedData(certificateBlob, out var signedData)
            ? signedData
            : null;
    }

    private static FileStream OpenTrustSnapshot(ReadOnlyMemory<byte> assemblyBytes, string path)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.Read,
            Options = FileOptions.SequentialScan
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        var stream = new FileStream(path, options);

        try
        {
            stream.Write(assemblyBytes.Span);
            stream.Flush(flushToDisk: true);
            return stream;
        }
        catch
        {
            stream.Dispose();
            File.Delete(path);
            throw;
        }
    }
}
