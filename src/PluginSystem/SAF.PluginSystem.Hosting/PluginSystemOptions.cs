// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using AssemblyLoading;

/// <summary>
/// Provides configuration options for the plugin system, including paths for plugin settings and search patterns for
/// plugin contracts (public types used for cross-plugin communication).
/// </summary>
/// <remarks>Use this class to specify file locations and search patterns required for plugin discovery and
/// configuration. These options are typically set during application startup to control how plugins are loaded and
/// managed.</remarks>
public class PluginSystemOptions
{
    /// <summary>
    /// Gets or sets the relative or absolute root directory path where plugin settings are stored.
    /// </summary>
    /// <remarks>The path can be relative or absolute. By default, it is set to "./config", which refers to a
    /// directory named "config" in the application's working directory.</remarks>
    public string PluginSettingsRootPath { get; set; } = "./config";

    /// <summary>
    /// Gets or sets the file path to the plugin settings configuration file.
    /// The path is relative to the <see cref="PluginSettingsRootPath"/>. By default, it is set to "./pluginsettings.json",
    /// which refers to a file named "pluginsettings.json" in the plugin settings root directory.
    /// </summary>
    public string PluginSettingsFilePath { get; set; } = "./pluginsettings.json";

    /// <summary>
    /// Gets or sets the search pattern used to locate plugin contract files (public plugin service types).
    /// </summary>
    /// <remarks>Specify a file name pattern, such as "*.dll", to filter which files are considered plugin
    /// contracts during discovery. The pattern should follow standard file system search conventions.
    /// Multiple patterns should be seperated with ';'</remarks>
    public string PluginContractsSearchPattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the behavior applied when a plugin requests a version of a shared (contract) assembly
    /// that is not compatible with the one the host provides. Defaults to
    /// <see cref="SharedAssemblyConflictBehavior.Fail"/>.
    /// </summary>
    public SharedAssemblyConflictBehavior SharedAssemblyConflictBehavior { get; set; } = SharedAssemblyConflictBehavior.Fail;

    /// <summary>
    /// Gets or sets whether a shared assembly may roll forward across a <b>major</b> version, i.e. whether
    /// a plugin built against major <c>N</c> may be bound to the host's major <c>&gt; N</c>. Since a major
    /// version signals breaking changes (SemVer), this defaults to <see langword="false"/>: a breaking
    /// roll-forward is treated as a conflict and handled per <see cref="SharedAssemblyConflictBehavior"/>.
    /// Set to <see langword="true"/> only if your major versions are known to stay compatible.
    /// Roll-forward within the same major (minor/patch) is always allowed.
    /// </summary>
    public bool AllowMajorVersionRollForward { get; set; }
}