// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions;

using System.Buffers.Binary;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

internal sealed class AuthenticodeSignatureReader : IAuthenticodeSignatureReader
{
    public bool TryGetSignatureInfo(string assemblyPath, out string? signerThumbprint, out bool hasValidDigitalSignature)
    {
        signerThumbprint = null;
        hasValidDigitalSignature = false;

        try
        {
            var signedData = TryReadAuthenticodeSignedData(assemblyPath);
            if (signedData is null)
            {
                return false;
            }

            var cms = new SignedCms();
            cms.Decode(signedData);
            cms.CheckSignature(verifySignatureOnly: true);

            var signerCertificate = cms.SignerInfos.Count > 0
                ? cms.SignerInfos[0].Certificate
                : cms.Certificates.OfType<X509Certificate2>().FirstOrDefault();

            if (signerCertificate is null)
            {
                return false;
            }

            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

            hasValidDigitalSignature = chain.Build(signerCertificate);
            signerThumbprint = signerCertificate.Thumbprint?.Replace(" ", string.Empty, StringComparison.Ordinal);
            return true;
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException
                                   or PlatformNotSupportedException
                                   or FileNotFoundException
                                   or DirectoryNotFoundException
                                   or IOException
                                   or BadImageFormatException)
        {
            return false;
        }
    }

    private static byte[]? TryReadAuthenticodeSignedData(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new System.Reflection.PortableExecutable.PEReader(stream);
        var certificateDirectory = peReader.PEHeaders.PEHeader?.CertificateTableDirectory;

        if (certificateDirectory is null ||
            certificateDirectory.Value.RelativeVirtualAddress <= 0 ||
            certificateDirectory.Value.Size <= 8)
        {
            return null;
        }

        stream.Position = certificateDirectory.Value.RelativeVirtualAddress;
        var certificateBlob = new byte[certificateDirectory.Value.Size];
        var bytesRead = stream.Read(certificateBlob, 0, certificateBlob.Length);
        if (bytesRead < certificateBlob.Length)
        {
            return null;
        }

        const ushort winCertificateTypePkcsSignedData = 0x0002;
        const int winCertificateHeaderSize = 8;
        var offset = 0;

        while (offset + winCertificateHeaderSize <= certificateBlob.Length)
        {
            var length = BinaryPrimitives.ReadInt32LittleEndian(certificateBlob.AsSpan(offset));
            if (length < winCertificateHeaderSize || offset + length > certificateBlob.Length)
            {
                break;
            }

            var certificateType = BinaryPrimitives.ReadUInt16LittleEndian(certificateBlob.AsSpan(offset + 6));
            if (certificateType == winCertificateTypePkcsSignedData)
            {
                return certificateBlob
                    .AsSpan(offset + winCertificateHeaderSize, length - winCertificateHeaderSize)
                    .ToArray();
            }

            offset += (length + 7) & ~7;
        }

        return null;
    }
}
