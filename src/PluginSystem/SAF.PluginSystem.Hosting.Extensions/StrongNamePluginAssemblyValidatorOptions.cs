// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions;

/// <summary>
/// Configures strong-name validation for plugin assemblies.
/// </summary>
public sealed class StrongNamePluginAssemblyValidatorOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether an assembly must have a strong name.
    /// </summary>
    public bool RequireStrongName { get; set; }

    /// <summary>
    /// Gets the case-insensitive allow-list of public key tokens.
    /// </summary>
    public ISet<string> AllowedPublicKeyTokens { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
