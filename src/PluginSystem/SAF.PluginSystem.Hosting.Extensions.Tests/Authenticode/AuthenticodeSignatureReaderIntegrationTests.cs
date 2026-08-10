// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests.Authenticode;

using SAF.PluginSystem.Hosting.Extensions.Authenticode;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography.X509Certificates;

public sealed class AuthenticodeSignatureReaderIntegrationTests
{
    private const string SignedAssemblyEnvironmentVariable = "SAF_AUTHENTICODE_SIGNED_ASSEMBLY";

    [Fact]
    public void ReadSignature_ValidatesSigntoolSignedAssembly_UsingWindowsTrust()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "This test covers the Windows WinVerifyTrust path.");
        var signedAssemblyPath = GetSignedAssemblyPath();
        Assert.SkipWhen(signedAssemblyPath is null, "The signtool-signed fixture is not configured.");

        using var trustedCertificate = TrustSignerCertificate(signedAssemblyPath!);
        var reader = new AuthenticodeSignatureReader();
        var result = reader.ReadSignature(signedAssemblyPath!);

        Assert.NotNull(result);
        Assert.Equal(ReadExpectedThumbprint(signedAssemblyPath!), result!.SignerThumbprint);
        Assert.True(result.HasValidDigitalSignature);
    }

    [Fact]
    public void ReadSignature_ValidatesSigntoolSignedAssembly_UsingLinuxChainTrust()
    {
        Assert.SkipUnless(!OperatingSystem.IsWindows(), "This test covers the non-Windows X509Chain path.");
        var signedAssemblyPath = GetSignedAssemblyPath();
        Assert.SkipWhen(signedAssemblyPath is null, "The signtool-signed fixture is not configured.");

        using var trustedCertificate = TrustSignerCertificate(signedAssemblyPath!);
        var reader = new AuthenticodeSignatureReader();
        var result = reader.ReadSignature(signedAssemblyPath!);

        Assert.NotNull(result);
        Assert.Equal(ReadExpectedThumbprint(signedAssemblyPath!), result!.SignerThumbprint);
        Assert.True(result.HasValidDigitalSignature);
    }

    [Fact]
    public void ReadSignature_RejectsSigntoolSignedAssembly_WhenFileIsTampered()
    {
        var signedAssemblyPath = GetSignedAssemblyPath();
        Assert.SkipWhen(signedAssemblyPath is null, "The signtool-signed fixture is not configured.");

        var tamperedAssemblyPath = CreateTamperedCopy(signedAssemblyPath!);
        try
        {
            var reader = new AuthenticodeSignatureReader();
            var result = reader.ReadSignature(tamperedAssemblyPath);

            Assert.Null(result?.SignerThumbprint);
            Assert.False(result?.HasValidDigitalSignature ?? false);
        }
        finally
        {
            File.Delete(tamperedAssemblyPath);
        }
    }

    [Fact]
    public void ReadSignature_ReportsSignerButRejectsUntrustedSigntoolCertificate_OnAllSupportedPlatforms()
    {
        var signedAssemblyPath = GetSignedAssemblyPath();
        Assert.SkipWhen(signedAssemblyPath is null, "The signtool-signed fixture is not configured.");

        var reader = new AuthenticodeSignatureReader();
        var result = reader.ReadSignature(signedAssemblyPath!);

        Assert.NotNull(result);
        Assert.Equal(ReadExpectedThumbprint(signedAssemblyPath!), result!.SignerThumbprint);
        Assert.False(result.HasValidDigitalSignature);
    }

    private static string? GetSignedAssemblyPath()
    {
        var path = Environment.GetEnvironmentVariable(SignedAssemblyEnvironmentVariable);
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;
    }

    private static string ReadExpectedThumbprint(string signedAssemblyPath)
    {
        using var certificate = X509CertificateLoader.LoadCertificateFromFile(GetCertificatePath(signedAssemblyPath));
        return certificate.Thumbprint ?? throw new InvalidOperationException("Signer thumbprint is missing.");
    }

    private static TrustedCertificateScope TrustSignerCertificate(string signedAssemblyPath)
    {
        var certificate = X509CertificateLoader.LoadCertificateFromFile(GetCertificatePath(signedAssemblyPath));
        var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
        try
        {
            store.Open(OpenFlags.ReadWrite);
            store.Add(certificate);
            return new TrustedCertificateScope(store, certificate);
        }
        catch
        {
            certificate.Dispose();
            store.Dispose();
            throw;
        }
    }

    private static string GetCertificatePath(string signedAssemblyPath)
        => Path.ChangeExtension(signedAssemblyPath, ".cer");

    private sealed class TrustedCertificateScope(X509Store store, X509Certificate2 certificate) : IDisposable
    {
        public void Dispose()
        {
            try
            {
                store.Remove(certificate);
            }
            finally
            {
                certificate.Dispose();
                store.Dispose();
            }
        }
    }

    private static string CreateTamperedCopy(string signedAssemblyPath)
    {
        var tamperedAssemblyPath = Path.Combine(Path.GetTempPath(), $"authenticode-tampered-{Guid.NewGuid():N}.dll");
        try
        {
            File.Copy(signedAssemblyPath, tamperedAssemblyPath);
            var bytes = File.ReadAllBytes(tamperedAssemblyPath);

            using var stream = new MemoryStream(bytes, writable: false);
            using var peReader = new PEReader(stream);
            var certificateTableOffset = peReader.PEHeaders.PEHeader?.CertificateTableDirectory.RelativeVirtualAddress
                ?? throw new InvalidOperationException("PE certificate table is missing.");
            if (certificateTableOffset <= 0 || certificateTableOffset > bytes.Length)
            {
                throw new InvalidOperationException("PE certificate table offset is invalid.");
            }

            bytes[certificateTableOffset - 1] ^= 0xFF;
            File.WriteAllBytes(tamperedAssemblyPath, bytes);
            return tamperedAssemblyPath;
        }
        catch
        {
            File.Delete(tamperedAssemblyPath);
            throw;
        }
    }

}
