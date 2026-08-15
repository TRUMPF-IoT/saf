// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Contracts;

/// <summary>
/// Builds the physical key an <see cref="ISecretStoreProvider"/> uses to store and look up a secret,
/// combining the configured namespace with the logical secret name. Shared by every provider so the
/// namespace convention has a single owner.
/// </summary>
public static class SecretTargetName
{
    /// <summary>
    /// Builds the target name for <paramref name="name"/> under <paramref name="ns"/>, normalized to a
    /// single, consistent case.
    /// </summary>
    /// <remarks>
    /// The Windows Credential Manager treats target names case-insensitively regardless of what is
    /// stored; without normalizing here, the same logical secret could resolve differently depending on
    /// which backend is active. Lower-invariant is the canonical form for every provider.
    /// </remarks>
    public static string Build(string? ns, string name)
        => (string.IsNullOrEmpty(ns) ? name : $"{ns}/{name}").ToLowerInvariant();
}
