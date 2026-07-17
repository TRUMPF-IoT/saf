// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions;

using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

internal sealed class AuthenticodeSignatureReader : IAuthenticodeSignatureReader
{
    private const int WinCertificateHeaderSize = 8;

    private readonly IAuthenticodeChainTrustVerifier _trustVerifier;

    public AuthenticodeSignatureReader()
        : this(OperatingSystem.IsWindows()
            ? new WindowsAuthenticodeTrustVerifier()
            : new CrossPlatformAuthenticodeTrustVerifier())
    {
    }

    internal AuthenticodeSignatureReader(IAuthenticodeChainTrustVerifier trustVerifier)
    {
        ArgumentNullException.ThrowIfNull(trustVerifier);
        _trustVerifier = trustVerifier;
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
                || AuthenticodePeHasher.VerifyEmbeddedHashMatchesFile(assemblyPath, cms);

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

    private static byte[]? TryReadAuthenticodeSignedData(string assemblyPath)
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

        // For the certificate table the data directory holds a file offset, not an RVA.
        stream.Position = certificateDirectory.Value.RelativeVirtualAddress;
        var certificateBlob = new byte[certificateDirectory.Value.Size];
        try
        {
            stream.ReadExactly(certificateBlob);
        }
        catch (EndOfStreamException)
        {
            return null;
        }

        return ExtractPkcsSignedData(certificateBlob);
    }

    private static byte[]? ExtractPkcsSignedData(byte[] certificateBlob)
    {
        const ushort winCertificateTypePkcsSignedData = 0x0002;
        var offset = 0;

        while (offset + WinCertificateHeaderSize <= certificateBlob.Length)
        {
            // WIN_CERTIFICATE: DWORD dwLength; WORD wRevision; WORD wCertificateType; BYTE bCertificate[].
            var length = BinaryPrimitives.ReadInt32LittleEndian(certificateBlob.AsSpan(offset));
            if (length < WinCertificateHeaderSize || offset + length > certificateBlob.Length)
            {
                break;
            }

            var certificateType = BinaryPrimitives.ReadUInt16LittleEndian(certificateBlob.AsSpan(offset + 6));
            if (certificateType == winCertificateTypePkcsSignedData)
            {
                return certificateBlob
                    .AsSpan(offset + WinCertificateHeaderSize, length - WinCertificateHeaderSize)
                    .ToArray();
            }

            offset += (length + 7) & ~7;
        }

        return null;
    }
}
