// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;
using System.Reflection;

/// <summary>
/// Loads a plugin manifest from a plugin assembly.
/// </summary>
public interface IPluginManifestLoader
{
    /// <summary>
    /// Finds and returns the plugin manifest implementation in the specified <paramref name="assembly"/>.
    /// </summary>
    /// <param name="assembly">The assembly to inspect.</param>
    /// <returns>
    /// The discovered <see cref="IPluginManifest"/> instance, or <see langword="null"/> when no manifest is available.
    /// </returns>
    IPluginManifest? LoadPluginManifest(Assembly assembly);
}