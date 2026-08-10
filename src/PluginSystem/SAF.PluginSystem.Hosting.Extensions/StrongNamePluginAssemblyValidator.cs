// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions;

using Microsoft.Extensions.Options;

/// <summary>
/// Enforces strong-name related plugin assembly trust checks.
/// </summary>
public sealed class StrongNamePluginAssemblyValidator : IPluginAssemblyValidator
{
    private readonly IOptionsMonitor<StrongNamePluginAssemblyValidatorOptions> _optionsMonitor;
    private readonly string _optionsName;

    public StrongNamePluginAssemblyValidator(IOptionsMonitor<StrongNamePluginAssemblyValidatorOptions> optionsMonitor)
        : this(optionsMonitor, Options.DefaultName)
    {
    }

    internal StrongNamePluginAssemblyValidator(
        IOptionsMonitor<StrongNamePluginAssemblyValidatorOptions> optionsMonitor,
        string optionsName)
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);
        ArgumentNullException.ThrowIfNull(optionsName);

        _optionsMonitor = optionsMonitor;
        _optionsName = optionsName;
    }

    public PluginAssemblyValidationResult Validate(PluginAssemblyValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = _optionsMonitor.Get(_optionsName);
        var publicKeyToken = GetPublicKeyToken(context.AssemblyName);

        if (options.RequireStrongName && string.IsNullOrWhiteSpace(publicKeyToken))
        {
            return PluginAssemblyValidationResult.Rejected("assembly is not strong-name signed");
        }

        if (options.AllowedPublicKeyTokens.Count > 0 &&
            (string.IsNullOrWhiteSpace(publicKeyToken) || !options.AllowedPublicKeyTokens.Contains(publicKeyToken)))
        {
            return PluginAssemblyValidationResult.Rejected("assembly public key token is not in the configured allow-list");
        }

        return PluginAssemblyValidationResult.Accepted();
    }

    private static string? GetPublicKeyToken(System.Reflection.AssemblyName assemblyName)
    {
        var token = assemblyName.GetPublicKeyToken();
        return token is { Length: > 0 }
            ? Convert.ToHexString(token).ToLowerInvariant()
            : null;
    }
}
