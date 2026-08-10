// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions;

using Microsoft.Extensions.Options;
using SAF.PluginSystem.Hosting.Extensions.Authenticode;

/// <summary>
/// Enforces digital-signature related plugin assembly trust checks.
/// </summary>
public sealed class DigitalSignaturePluginAssemblyValidator : IPluginAssemblyValidator
{
    private readonly DigitalSignaturePluginAssemblyValidatorOptions _options;
    private readonly IAuthenticodeSignatureReader _authenticodeSignatureReader;

    public DigitalSignaturePluginAssemblyValidator(IOptions<DigitalSignaturePluginAssemblyValidatorOptions> options)
        : this(options, new AuthenticodeSignatureReader())
    {
    }

    internal DigitalSignaturePluginAssemblyValidator(
        IOptions<DigitalSignaturePluginAssemblyValidatorOptions> options,
        IAuthenticodeSignatureReader authenticodeSignatureReader)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(authenticodeSignatureReader);

        _options = options.Value;
        _authenticodeSignatureReader = authenticodeSignatureReader;
    }

    public PluginAssemblyValidationResult Validate(PluginAssemblyValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var signature = context.AssemblyBytes.IsEmpty
            ? _authenticodeSignatureReader.ReadSignature(context.AssemblyPath)
            : _authenticodeSignatureReader.ReadSignature(context.AssemblyBytes);
        var signerThumbprint = signature?.SignerThumbprint;
        var hasValidDigitalSignature = signature?.HasValidDigitalSignature ?? false;

        if (_options.RequireValidDigitalSignature && !hasValidDigitalSignature)
        {
            return PluginAssemblyValidationResult.Rejected("assembly does not have a valid digital signature");
        }

        if (_options.AllowedSignerThumbprints.Count > 0 &&
            (string.IsNullOrWhiteSpace(signerThumbprint) || !_options.AllowedSignerThumbprints.Contains(signerThumbprint)))
        {
            return PluginAssemblyValidationResult.Rejected("assembly signer thumbprint is not in the configured allow-list");
        }

        return PluginAssemblyValidationResult.Accepted();
    }
}
