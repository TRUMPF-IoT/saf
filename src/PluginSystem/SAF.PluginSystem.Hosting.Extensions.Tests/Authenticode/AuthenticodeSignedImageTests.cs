// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests.Authenticode;

using SAF.PluginSystem.Hosting.Extensions.Authenticode;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

/// <summary>
/// End-to-end coverage of the accepted path: a real Authenticode-signed image, read through the same
/// pipeline production uses, on every operating system.
/// </summary>
/// <remarks>
/// Trust is anchored in a throwaway root, so the verifier under test is the cross-platform one even on
/// Windows - <c>WinVerifyTrust</c> would never accept a root this test made up. The Windows API is
/// covered against a genuinely trusted signature by the runtime-assembly test, and against an untrusted
/// one by the signtool fixture.
/// </remarks>
public sealed class AuthenticodeSignedImageTests : IDisposable
{
    private readonly AuthenticodeTestPki _pki = new();
    private readonly List<string> _temporaryFiles = [];

    [Theory]
    [InlineData("SHA256")]
    [InlineData("SHA384")]
    [InlineData("SHA512")]
    public void ReadSignature_ReportsATrustedSignature_ForASignedImage(string hashAlgorithmName)
    {
        using var signer = _pki.IssueEndEntityCertificate("CN=SAF Authenticode image signer");
        var signedImage = SignTestImage(signer, new HashAlgorithmName(hashAlgorithmName));
        var reader = CreateReaderTrusting(_pki);

        var fromFile = reader.ReadSignature(WriteTemporaryFile(signedImage));
        var fromSnapshot = reader.ReadSignature(signedImage);

        Assert.Equal(signer.Thumbprint, fromFile?.SignerThumbprint);
        Assert.True(fromFile?.HasValidDigitalSignature);
        Assert.Equal(signer.Thumbprint, fromSnapshot?.SignerThumbprint);
        Assert.True(fromSnapshot?.HasValidDigitalSignature);
    }

    [Fact]
    public void ReadSignature_ReportsTheSigner_ButNoTrust_WhenTheRootIsUnknown()
    {
        using var signer = _pki.IssueEndEntityCertificate("CN=SAF Authenticode untrusted image signer");
        var signedImage = SignTestImage(signer);

        // The stock reader anchors trust in the machine stores, which carry no root of this test's making.
        var result = new AuthenticodeSignatureReader().ReadSignature(WriteTemporaryFile(signedImage));

        // The signature still verifiably covers the image, which is why the thumbprint is reported.
        Assert.Equal(signer.Thumbprint, result?.SignerThumbprint);
        Assert.False(result?.HasValidDigitalSignature);
    }

    [Fact]
    public void ReadSignature_RejectsASignedImage_ThatWasModifiedAfterSigning()
    {
        using var signer = _pki.IssueEndEntityCertificate("CN=SAF Authenticode tampered image signer");
        var signedImage = SignTestImage(signer);
        signedImage[signedImage.Length / 2] ^= 0xFF;
        var reader = CreateReaderTrusting(_pki);

        var fromFile = reader.ReadSignature(WriteTemporaryFile(signedImage));
        var fromSnapshot = reader.ReadSignature(signedImage);

        // Trust alone must never be enough: the recomputed PE hash has to bind the signature to the image.
        Assert.Null(fromFile?.SignerThumbprint);
        Assert.False(fromFile?.HasValidDigitalSignature ?? false);
        Assert.Null(fromSnapshot?.SignerThumbprint);
        Assert.False(fromSnapshot?.HasValidDigitalSignature ?? false);
    }

    [Fact]
    public void ReadSignature_RejectsASignature_TransplantedFromAnotherImage()
    {
        using var signer = _pki.IssueEndEntityCertificate("CN=SAF Authenticode transplant signer");
        var unsignedImage = ReadUnsignedTestImage();
        var signedImage = AuthenticodeTestPeSigner.Sign(unsignedImage, signer, [_pki.Intermediate]);

        // Same signature, a different image underneath it.
        var foreignImage = (byte[])unsignedImage.Clone();
        foreignImage[foreignImage.Length / 3] ^= 0xFF;
        var transplanted = TransplantCertificateTable(signedImage, foreignImage);

        var result = CreateReaderTrusting(_pki).ReadSignature(WriteTemporaryFile(transplanted));

        Assert.Null(result?.SignerThumbprint);
        Assert.False(result?.HasValidDigitalSignature ?? false);
    }

    [Fact]
    public void ReadSignature_RejectsASignedImage_WhenTheSignerMayNotSignCode()
    {
        using var signer = _pki.IssueEndEntityCertificate(
            "CN=SAF Authenticode wrong usage signer",
            enhancedKeyUsageOid: AuthenticodeTestPki.TimeStampingOid);
        var signedImage = SignTestImage(signer);
        var reader = CreateReaderTrusting(_pki);

        var result = reader.ReadSignature(WriteTemporaryFile(signedImage));

        // The image is intact, so the thumbprint is reported; the verdict is not.
        Assert.Equal(signer.Thumbprint, result?.SignerThumbprint);
        Assert.False(result?.HasValidDigitalSignature);
    }

    [Fact]
    public void ReadSignature_RejectsASignedImage_WhenTheIntermediateIsMissing()
    {
        using var signer = _pki.IssueEndEntityCertificate("CN=SAF Authenticode orphan image signer");
        var signedImage = AuthenticodeTestPeSigner.Sign(ReadUnsignedTestImage(), signer, []);
        var reader = CreateReaderTrusting(_pki);

        var result = reader.ReadSignature(WriteTemporaryFile(signedImage));

        Assert.Equal(signer.Thumbprint, result?.SignerThumbprint);
        Assert.False(result?.HasValidDigitalSignature);
    }

    [Fact]
    public void ReadSignature_AcceptsASignedImage_WhenAnExpiredSignerIsCounterSigned()
    {
        using var signer = _pki.IssueEndEntityCertificate(
            "CN=SAF Authenticode expired image signer",
            notBefore: DateTimeOffset.UtcNow.AddDays(-10),
            notAfter: DateTimeOffset.UtcNow.AddDays(-1));

        var signedImage = AuthenticodeTestPeSigner.Sign(
            ReadUnsignedTestImage(),
            signer,
            [_pki.Intermediate],
            decorateSignature: cms => AuthenticodeTestTimestamp.AddCounterSignature(
                cms, signer, DateTimeOffset.UtcNow.AddDays(-5)));

        var result = CreateReaderTrusting(_pki).ReadSignature(WriteTemporaryFile(signedImage));

        Assert.True(result?.HasValidDigitalSignature);
    }

    [Fact]
    public void ReadSignature_AcceptsASignedImage_WhenAnExpiredSignerIsRfc3161Timestamped()
    {
        using var signer = _pki.IssueEndEntityCertificate(
            "CN=SAF Authenticode timestamped image signer",
            notBefore: DateTimeOffset.UtcNow.AddDays(-10),
            notAfter: DateTimeOffset.UtcNow.AddDays(-1));
        using var timestampAuthority = _pki.IssueTimestampAuthorityCertificate();

        var signedImage = AuthenticodeTestPeSigner.Sign(
            ReadUnsignedTestImage(),
            signer,
            [_pki.Intermediate],
            decorateSignature: cms => AuthenticodeTestTimestamp.AddRfc3161Timestamp(
                cms, timestampAuthority, DateTimeOffset.UtcNow.AddDays(-5)));

        var result = CreateReaderTrusting(_pki).ReadSignature(WriteTemporaryFile(signedImage));

        // The form a signtool signature actually uses, end to end through the reader.
        Assert.True(result?.HasValidDigitalSignature);
    }

    public void Dispose()
    {
        foreach (var path in _temporaryFiles)
        {
            File.Delete(path);
        }

        _pki.Dispose();
    }

    private static AuthenticodeSignatureReader CreateReaderTrusting(AuthenticodeTestPki pki)
        => new(new CrossPlatformAuthenticodeTrustVerifier(pki.TrustAnchors));

    private byte[] SignTestImage(X509Certificate2 signer, HashAlgorithmName? hashAlgorithm = null)
        => AuthenticodeTestPeSigner.Sign(ReadUnsignedTestImage(), signer, [_pki.Intermediate], hashAlgorithm);

    private static byte[] ReadUnsignedTestImage()
        => AuthenticodeTestPeSigner.ReadUnsignedImage(typeof(AuthenticodeSignedImageTests).Assembly.Location);

    // Copies the certificate table of a signed image onto a different image, keeping the data directory
    // consistent, which is the shape a transplanted signature has.
    private static byte[] TransplantCertificateTable(byte[] signedImage, byte[] targetImage)
    {
        using var stream = new MemoryStream(signedImage, writable: false);
        using var peReader = new PEReader(stream);
        var directory = peReader.PEHeaders.PEHeader!.CertificateTableDirectory;
        var table = signedImage.AsSpan(directory.RelativeVirtualAddress, directory.Size).ToArray();

        var peHeaders = peReader.PEHeaders;
        var dataDirectoriesOffset = peHeaders.PEHeaderStartOffset +
            (peHeaders.PEHeader!.Magic == PEMagic.PE32Plus ? 112 : 96);
        var certificateTableEntryOffset = dataDirectoriesOffset + (4 * 8);

        var result = new byte[targetImage.Length + table.Length];
        targetImage.CopyTo(result, 0);
        table.CopyTo(result, targetImage.Length);

        var directoryEntry = result.AsSpan(certificateTableEntryOffset, 8);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(directoryEntry, targetImage.Length);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(directoryEntry[4..], table.Length);
        return result;
    }

    private string WriteTemporaryFile(byte[] image)
    {
        var path = Path.Combine(Path.GetTempPath(), $"authenticode-signed-{Guid.NewGuid():N}.dll");
        File.WriteAllBytes(path, image);
        _temporaryFiles.Add(path);
        return path;
    }
}
