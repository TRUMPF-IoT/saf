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
    private readonly IAuthenticodeChainTrustVerifier _contentTrustVerifier;
    private readonly IAuthenticodePeHasher _peHasher;
    private readonly IAuthenticodeCertificateTableParser _certificateTableParser;

    /// <summary>
    /// Initializes a signature reader from its collaborators.
    /// </summary>
    /// <remarks>
    /// The only constructor on purpose: every convenience overload that assembled the collaborators itself
    /// was a second composition root beside the service registration, free to drift away from it unnoticed.
    /// </remarks>
    public AuthenticodeSignatureReader(
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

        // A verifier that reads the file cannot serve a caller that only holds a content snapshot, and
        // materializing the snapshot just to satisfy it would make signature checking depend on a
        // writable temp directory. Chain building works on the decoded signature alone, so the snapshot
        // route uses it and pairs it with the in-memory PE hash check for the file binding.
        _contentTrustVerifier = trustVerifier.RequiresFilePath
            ? new CrossPlatformAuthenticodeTrustVerifier()
            : trustVerifier;
    }

    /// <inheritdoc />
    public AuthenticodeSignatureInfo? ReadSignature(string assemblyPath)
    {
        try
        {
            return ReadSignatureCore(
                _trustVerifier,
                TryReadAuthenticodeSignedData(assemblyPath),
                assemblyPath,
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
            // Verified entirely in memory: the signature math from the decoded CMS, the file binding from
            // the PE hash recomputed over the snapshot, and the trust anchor from the certificate chain.
            // Nothing is written to disk, so signature checking cannot fail on a locked-down temp path.
            return ReadSignatureCore(
                _contentTrustVerifier,
                TryReadAuthenticodeSignedData(assemblyBytes),
                assemblyPath: null,
                cms => _peHasher.VerifyEmbeddedHashMatchesFile(assemblyBytes, cms));
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

    private static AuthenticodeSignatureInfo? ReadSignatureCore(
        IAuthenticodeChainTrustVerifier trustVerifier,
        byte[]? signedData,
        string? assemblyPath,
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

        var isTrusted = trustVerifier.IsTrusted(assemblyPath, cms);

        var signatureCoversFile = (isTrusted && trustVerifier.VerifiesFileIntegrity)
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
        using var stream = SnapshotStream.Create(assemblyBytes);
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
}
