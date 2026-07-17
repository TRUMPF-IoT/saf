// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

/// <summary>
/// Enforces digital-signature related plugin assembly trust checks.
/// </summary>
public sealed class DigitalSignaturePluginAssemblyValidator(DigitalSignaturePluginAssemblyValidatorOptions options) : IPluginAssemblyValidator
{
    private readonly DigitalSignaturePluginAssemblyValidatorOptions _options = options;

    public PluginAssemblyValidationResult Validate(PluginAssemblyValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var signerThumbprint = TryGetSignerThumbprint(context.AssemblyPath, out var hasValidDigitalSignature);

        if (_options.RequireValidDigitalSignature && !hasValidDigitalSignature)
        {
            return PluginAssemblyValidationResult.Rejected("assembly does not have a valid digital signature");
        }

        if (_options.AllowedSignerThumbprints.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(signerThumbprint) || !_options.AllowedSignerThumbprints.Contains(signerThumbprint))
            {
                return PluginAssemblyValidationResult.Rejected("assembly signer thumbprint is not in the configured allow-list");
            }
        }

        return PluginAssemblyValidationResult.Accepted();
    }

    private static string? TryGetSignerThumbprint(string assemblyPath, out bool hasValidDigitalSignature)
    {
        hasValidDigitalSignature = false;

        try
        {
            var signerCertificate = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(assemblyPath);
            if (signerCertificate is null)
            {
                return null;
            }

            using var signerCertificate2 = new System.Security.Cryptography.X509Certificates.X509Certificate2(signerCertificate);
            using var chain = new System.Security.Cryptography.X509Certificates.X509Chain();
            chain.ChainPolicy.RevocationMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck;
            chain.ChainPolicy.RevocationFlag = System.Security.Cryptography.X509Certificates.X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.VerificationFlags = System.Security.Cryptography.X509Certificates.X509VerificationFlags.NoFlag;

            hasValidDigitalSignature = chain.Build(signerCertificate2);
            return signerCertificate2.Thumbprint?.Replace(" ", string.Empty, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or PlatformNotSupportedException)
        {
            return null;
        }
    }
}
