// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Contracts;

/// <summary>
/// Provides runtime environment context for the plugin system, including the
/// environment name and the root path for plugin settings files.
/// </summary>
public interface IPluginSystemHostEnvironment
{
    /// <summary>
    /// Gets the name of the current hosting environment (e.g. <c>"Development"</c>, <c>"bmw"</c>).
    /// Used to select environment-specific configuration overlays.
    /// </summary>
    string EnvironmentName { get; }

    /// <summary>
    /// Gets or sets the base directory path from which all plugin-related configuration
    /// file paths are resolved.
    /// </summary>
    string PluginSettingsRootPath { get; set; }
}