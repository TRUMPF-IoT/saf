// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests.Authenticode;

using SAF.PluginSystem.Hosting.Extensions.Authenticode;
using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Verifies the signtool fixture against the platform's own trust store, after CI has installed the
/// fixture's certificate as a trusted root.
/// </summary>
/// <remarks>
/// This is the one place where the production verifiers decide a *trusted* verdict on their own terms:
/// <c>WinVerifyTrust</c> against the Windows root store on Windows, and <see cref="X509Chain"/> against
/// the OpenSSL bundle on Linux. Everything else either asserts a rejection, or supplies its own trust
/// anchors. Installing a root is a machine-wide change, so the tests stay skipped unless the environment
/// says the CI job has done it; running them on a developer machine would prove nothing anyway, since
/// the certificate would not be trusted there.
/// </remarks>
public sealed class AuthenticodeTrustedRootIntegrationTests
{
    private const string SignedAssemblyEnvironmentVariable = "SAF_AUTHENTICODE_SIGNED_ASSEMBLY";
    private const string TrustedRootEnvironmentVariable = "SAF_AUTHENTICODE_TRUSTED_ROOT";

    [Fact]
    public void ReadSignature_ReportsATrustedSignature_ThroughThePlatformTrustStore()
    {
        var signedAssemblyPath = GetTrustedFixturePath();
        Assert.SkipWhen(signedAssemblyPath is null, "The fixture certificate is not installed as a trusted root.");

        var result = AuthenticodeReaderFactory.CreateDefault().ReadSignature(signedAssemblyPath!);

        Assert.Equal(ReadExpectedThumbprint(signedAssemblyPath!), result?.SignerThumbprint);
        Assert.True(result?.HasValidDigitalSignature);
    }

    [Fact]
    public void ReadSignature_ReportsATrustedSignature_FromAContentSnapshot()
    {
        var signedAssemblyPath = GetTrustedFixturePath();
        Assert.SkipWhen(signedAssemblyPath is null, "The fixture certificate is not installed as a trusted root.");

        var result = AuthenticodeReaderFactory.CreateDefault().ReadSignature(File.ReadAllBytes(signedAssemblyPath!));

        // The snapshot route builds the chain itself and must reach the same verdict as the file route.
        Assert.Equal(ReadExpectedThumbprint(signedAssemblyPath!), result?.SignerThumbprint);
        Assert.True(result?.HasValidDigitalSignature);
    }

    [Fact]
    public void ReadSignature_RejectsTheFixture_WhenItWasModifiedAfterSigning()
    {
        var signedAssemblyPath = GetTrustedFixturePath();
        Assert.SkipWhen(signedAssemblyPath is null, "The fixture certificate is not installed as a trusted root.");

        var tamperedPath = Path.Combine(Path.GetTempPath(), $"authenticode-trusted-tampered-{Guid.NewGuid():N}.dll");
        try
        {
            var bytes = File.ReadAllBytes(signedAssemblyPath!);
            bytes[bytes.Length / 2] ^= 0xFF;
            File.WriteAllBytes(tamperedPath, bytes);

            var result = AuthenticodeReaderFactory.CreateDefault().ReadSignature(tamperedPath);

            // A trusted signer must not carry a modified image with it.
            Assert.Null(result?.SignerThumbprint);
            Assert.False(result?.HasValidDigitalSignature ?? false);
        }
        finally
        {
            File.Delete(tamperedPath);
        }
    }

    private static string? GetTrustedFixturePath()
    {
        if (Environment.GetEnvironmentVariable(TrustedRootEnvironmentVariable) != "1")
        {
            return null;
        }

        var path = Environment.GetEnvironmentVariable(SignedAssemblyEnvironmentVariable);
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;
    }

    private static string ReadExpectedThumbprint(string signedAssemblyPath)
    {
        using var certificate = X509CertificateLoader.LoadCertificateFromFile(
            Path.ChangeExtension(signedAssemblyPath, ".cer"));
        return certificate.Thumbprint ?? throw new InvalidOperationException("Signer thumbprint is missing.");
    }
}
