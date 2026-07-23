// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.AssemblyLoading;

/// <summary>
/// Holds the set of assemblies that must be shared between the host and all plugin load contexts: the
/// transitive closure of the plugin contract assemblies plus any explicitly configured additions.
/// Lookups are performed by simple assembly name.
/// </summary>
internal interface ISharedAssemblyRegistry
{
    /// <summary>
    /// Attempts to get the shared-assembly information for the given simple assembly name.
    /// </summary>
    /// <param name="simpleName">The simple assembly name (without version or file extension).</param>
    /// <param name="info">The host-provided version and public key token, if the assembly is shared.</param>
    /// <returns><see langword="true"/> if the assembly is part of the shared set; otherwise <see langword="false"/>.</returns>
    bool TryGetSharedAssembly(string simpleName, out SharedAssemblyInfo info);

    /// <summary>
    /// Gets a snapshot of all shared assemblies keyed by simple name. Primarily intended for diagnostics.
    /// </summary>
    IReadOnlyDictionary<string, SharedAssemblyInfo> GetSharedAssemblies();
}
