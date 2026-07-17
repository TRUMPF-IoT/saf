// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions;
/// <summary>
/// Enforces strong-name related plugin assembly trust checks.
/// </summary>
public sealed class StrongNamePluginAssemblyValidator(StrongNamePluginAssemblyValidatorOptions options) : IPluginAssemblyValidator
{
    private readonly StrongNamePluginAssemblyValidatorOptions _options = options;

    public PluginAssemblyValidationResult Validate(PluginAssemblyValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var publicKeyToken = GetPublicKeyToken(context.AssemblyName);

        if (_options.RequireStrongName && string.IsNullOrWhiteSpace(publicKeyToken))
        {
            return PluginAssemblyValidationResult.Rejected("assembly is not strong-name signed");
        }

        if (_options.AllowedPublicKeyTokens.Count > 0 &&
            (string.IsNullOrWhiteSpace(publicKeyToken) || !_options.AllowedPublicKeyTokens.Contains(publicKeyToken)))
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
