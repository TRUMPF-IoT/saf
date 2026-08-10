// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions;

/// <summary>
/// Configures Authenticode validation for plugin assemblies.
/// </summary>
public sealed class DigitalSignaturePluginAssemblyValidatorOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether an assembly must have a valid digital signature.
    /// </summary>
    public bool RequireValidDigitalSignature { get; set; }

    /// <summary>
    /// Gets the case-insensitive allow-list of signer certificate thumbprints.
    /// </summary>
    public ISet<string> AllowedSignerThumbprints { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
