// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.AssemblyLoading;

/// <summary>
/// Describes how a plugin-requested assembly should be resolved with regard to the set of shared
/// (contract) assemblies.
/// </summary>
/// <remarks>
/// The values are assigned explicitly so that isolation stays the default: an
/// <see cref="ISharedAssemblyResolver"/> that returns an uninitialized decision isolates the assembly
/// instead of silently sharing it and dropping the isolation boundary.
/// </remarks>
public enum SharedAssemblyDecision
{
    /// <summary>
    /// The assembly is not shared (or not compatible for sharing) and must be loaded privately into
    /// the isolated plugin context.
    /// </summary>
    LoadIsolated = 0,

    /// <summary>
    /// The assembly is part of the shared set and a compatible (equal or higher) host version is
    /// available. The plugin context should defer loading to the default context so that the type
    /// identity is shared across the plugin boundary.
    /// </summary>
    ShareFromDefault = 1,

    /// <summary>
    /// The assembly is part of the shared set, but the host only provides an older version than the
    /// one requested by the plugin. Sharing would break the plugin, isolating would break the
    /// contract boundary.
    /// </summary>
    Conflict = 2
}
