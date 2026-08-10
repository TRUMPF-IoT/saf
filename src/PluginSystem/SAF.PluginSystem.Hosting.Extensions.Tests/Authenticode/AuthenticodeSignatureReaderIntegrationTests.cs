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
    public void ReadSignature_ValidatesTrustedDotNetRuntimeAssembly_UsingWindowsTrust()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "This test covers the Windows WinVerifyTrust path.");
        var runtimeAssemblyPath = FindTrustedDotNetRuntimeAssembly();
        Assert.SkipWhen(runtimeAssemblyPath is null, "No trusted Authenticode-signed .NET runtime assembly is available.");

        var reader = new AuthenticodeSignatureReader();
        var result = reader.ReadSignature(runtimeAssemblyPath!);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result!.SignerThumbprint));
        Assert.True(result.HasValidDigitalSignature);
    }

    [Fact]
    public void ReadSignature_ValidatesTrustedDotNetRuntimeAssembly_UsingLinuxChainTrust()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "This test covers the Linux X509Chain path.");
        var runtimeAssemblyPath = FindTrustedDotNetRuntimeAssembly();
        Assert.SkipWhen(runtimeAssemblyPath is null, "No trusted Authenticode-signed .NET runtime assembly is available.");

        var reader = new AuthenticodeSignatureReader();
        var result = reader.ReadSignature(runtimeAssemblyPath!);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result!.SignerThumbprint));
        Assert.True(result.HasValidDigitalSignature);
    }

    [Fact]
    public void ReadSignature_RejectsUntrustedSigntoolAssembly_UsingWindowsTrust()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "This test covers the Windows WinVerifyTrust path.");
        var signedAssemblyPath = GetSignedAssemblyPath();
        Assert.SkipWhen(signedAssemblyPath is null, "The signtool-signed fixture is not configured.");

        var reader = new AuthenticodeSignatureReader();
        var result = reader.ReadSignature(signedAssemblyPath!);

        Assert.NotNull(result);
        Assert.Equal(ReadExpectedThumbprint(signedAssemblyPath!), result!.SignerThumbprint);
        Assert.False(result.HasValidDigitalSignature);
    }

    [Fact]
    public void ReadSignature_RejectsUntrustedSigntoolAssembly_UsingLinuxChainTrust()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "This test covers the Linux X509Chain path.");
        var signedAssemblyPath = GetSignedAssemblyPath();
        Assert.SkipWhen(signedAssemblyPath is null, "The signtool-signed fixture is not configured.");

        var reader = new AuthenticodeSignatureReader();
        var result = reader.ReadSignature(signedAssemblyPath!);

        Assert.NotNull(result);
        Assert.Equal(ReadExpectedThumbprint(signedAssemblyPath!), result!.SignerThumbprint);
        Assert.False(result.HasValidDigitalSignature);
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

    private static string? GetSignedAssemblyPath()
    {
        var path = Environment.GetEnvironmentVariable(SignedAssemblyEnvironmentVariable);
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;
    }

    private static string? FindTrustedDotNetRuntimeAssembly()
    {
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (string.IsNullOrWhiteSpace(runtimeDirectory) || !Directory.Exists(runtimeDirectory))
        {
            return null;
        }

        var reader = new AuthenticodeSignatureReader();
        foreach (var candidate in Directory.EnumerateFiles(runtimeDirectory, "*.dll"))
        {
            if (reader.ReadSignature(candidate) is { SignerThumbprint: { Length: > 0 } })
            {
                return candidate;
            }
        }

        return null;
    }

    private static string ReadExpectedThumbprint(string signedAssemblyPath)
    {
        using var certificate = X509CertificateLoader.LoadCertificateFromFile(GetCertificatePath(signedAssemblyPath));
        return certificate.Thumbprint ?? throw new InvalidOperationException("Signer thumbprint is missing.");
    }

    private static string GetCertificatePath(string signedAssemblyPath)
        => Path.ChangeExtension(signedAssemblyPath, ".cer");

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
