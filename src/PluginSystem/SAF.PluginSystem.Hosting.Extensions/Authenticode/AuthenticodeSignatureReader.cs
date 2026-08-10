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

    public AuthenticodeSignatureInfo? ReadSignature(string assemblyPath)
    {
        try
        {
            var signedData = TryReadAuthenticodeSignedData(assemblyPath);
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

            var isTrusted = _trustVerifier.IsTrusted(assemblyPath, signerCertificate);

            // The signer's identity may only be trusted once we know the signature actually covers
            // this file. Only verifiers that hash the file themselves (Windows/WinVerifyTrust) let us
            // infer coverage from trust; otherwise we must always compare the embedded PE hash.
            var signatureCoversFile = (isTrusted && _trustVerifier.VerifiesFileIntegrity)
                || _peHasher.VerifyEmbeddedHashMatchesFile(assemblyPath, cms);

            var signerThumbprint = signatureCoversFile ? NormalizeThumbprint(signerCertificate.Thumbprint) : null;
            return new AuthenticodeSignatureInfo(signerThumbprint, isTrusted && signatureCoversFile);
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

    private static X509Certificate2? ResolveSignerCertificate(SignedCms cms)
    {
        var signerInfos = cms.SignerInfos;
        if (signerInfos.Count == 0)
        {
            return null;
        }

        // Authenticode always embeds the signing certificate, so use it directly. If it is missing
        // we cannot identify the signer safely - falling back to an arbitrary certificate in the bag
        // could select an unrelated intermediate CA - so the signature is treated as unusable.
        return signerInfos[0].Certificate;
    }

    private static string? NormalizeThumbprint(string? thumbprint)
        => string.IsNullOrWhiteSpace(thumbprint)
            ? null
            : thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal);

    private byte[]? TryReadAuthenticodeSignedData(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
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
