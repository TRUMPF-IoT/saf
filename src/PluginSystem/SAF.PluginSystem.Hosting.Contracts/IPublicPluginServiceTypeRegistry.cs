// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace SAF.PluginSystem.Hosting.Contracts;

using System.Collections.Generic;

/// <summary>
/// Provides a registry for accessing the full names of assemblies that contain public plugin service types.
/// </summary>
/// <remarks>This interface is used by the plugin framework to discover and enumerate assemblies that
/// expose public service types for plugins.</remarks>
public interface IPublicServiceTypeRegistry
{
    /// <summary>
    /// Retrieves the names of all assemblies available in the current context.
    /// </summary>
    /// <returns>An enumerable collection of strings containing the names of the assemblies. The collection will be empty if no
    /// assemblies are available.</returns>
    IEnumerable<string> GetAssemblyNames();
}