// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions;

using Microsoft.Extensions.Options;
using SAF.PluginSystem.Hosting.Contracts;
using SAF.PluginSystem.Hosting.Extensions.Authenticode;

/// <summary>
/// Enforces digital-signature related plugin assembly trust checks.
/// </summary>
public sealed class DigitalSignaturePluginAssemblyValidator : IPluginAssemblyValidator
{
    private readonly IOptionsMonitor<DigitalSignaturePluginAssemblyValidatorOptions> _optionsMonitor;
    private readonly string _optionsName;
    private readonly IAuthenticodeSignatureReader _authenticodeSignatureReader;

    /// <summary>
    /// Initializes a digital-signature plugin assembly validator.
    /// </summary>
    /// <param name="optionsMonitor">The monitor that supplies validator options.</param>
    public DigitalSignaturePluginAssemblyValidator(IOptionsMonitor<DigitalSignaturePluginAssemblyValidatorOptions> optionsMonitor)
        : this(optionsMonitor, Options.DefaultName, new AuthenticodeSignatureReader())
    {
    }

    internal DigitalSignaturePluginAssemblyValidator(
        IOptionsMonitor<DigitalSignaturePluginAssemblyValidatorOptions> optionsMonitor,
        string optionsName,
        IAuthenticodeSignatureReader authenticodeSignatureReader)
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);
        ArgumentNullException.ThrowIfNull(optionsName);
        ArgumentNullException.ThrowIfNull(authenticodeSignatureReader);

        _optionsMonitor = optionsMonitor;
        _optionsName = optionsName;
        _authenticodeSignatureReader = authenticodeSignatureReader;
    }

    /// <inheritdoc />
    public PluginAssemblyValidationResult Validate(PluginAssemblyValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = _optionsMonitor.Get(_optionsName);

        // Prefer the path: the hosting pipeline rejects a candidate whose file diverges from the snapshot
        // before loading it, so reading the file avoids materializing the snapshot for path-based verifiers.
        var signature = string.IsNullOrEmpty(context.AssemblyPath)
            ? _authenticodeSignatureReader.ReadSignature(context.AssemblyBytes)
            : _authenticodeSignatureReader.ReadSignature(context.AssemblyPath);
        var signerThumbprint = signature?.SignerThumbprint;
        var hasValidDigitalSignature = signature?.HasValidDigitalSignature ?? false;

        if (options.RequireValidDigitalSignature && !hasValidDigitalSignature)
        {
            return PluginAssemblyValidationResult.Rejected("assembly does not have a valid digital signature");
        }

        if (options.AllowedSignerThumbprints.Count > 0 &&
            (string.IsNullOrWhiteSpace(signerThumbprint) || !options.AllowedSignerThumbprints.Contains(signerThumbprint)))
        {
            return PluginAssemblyValidationResult.Rejected("assembly signer thumbprint is not in the configured allow-list");
        }

        return PluginAssemblyValidationResult.Accepted();
    }
}
