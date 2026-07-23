// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

/// <summary>
/// Compares the version of a shared assembly provided by the host with the version requested by a plugin.
/// </summary>
internal interface ISharedAssemblyVersionComparer
{
    /// <summary>
    /// Compares the host version with the requested version.
    /// </summary>
    /// <param name="hostVersion">The version the host provides. Must not be <see langword="null"/>.</param>
    /// <param name="requestedVersion">
    /// The version the plugin requests. A <see langword="null"/> value is treated as the lowest
    /// possible version (i.e. any host version satisfies it).
    /// </param>
    /// <returns>The relation of the host version to the requested version.</returns>
    SharedAssemblyVersionRelation Compare(Version hostVersion, Version? requestedVersion);
}
