// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Contracts;

/// <summary>
/// Provides access to all discovered <see cref="IPluginManifest"/> instances during assembly scanning.
/// </summary>
public interface IPluginAssemblyContainer
{
    /// <summary>
    /// Gets the <see cref="IPluginManifest"/> instances discovered from scanned assemblies.
    /// </summary>
    /// <returns>An enumerable of <see cref="IPluginManifest"/> instances, one per discovered plugin assembly.</returns>
    IEnumerable<IPluginManifest> GetPluginManifests();
}