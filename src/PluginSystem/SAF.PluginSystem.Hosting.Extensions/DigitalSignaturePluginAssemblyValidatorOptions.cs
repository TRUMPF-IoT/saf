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
    /// Defaults to <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Switching this off is only meaningful together with <see cref="AllowedSignerThumbprints"/>, which
    /// still demands a signature that covers the file - it just skips the trust chain. A validator with
    /// both checks off accepts every assembly, including unsigned ones, and is rejected at startup.
    /// </remarks>
    public bool RequireValidDigitalSignature { get; set; } = true;

    /// <summary>
    /// Gets the case-insensitive allow-list of signer certificate thumbprints.
    /// </summary>
    public ISet<string> AllowedSignerThumbprints { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
