// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

/// <summary>
/// Provides a custom plugin configuration source callback with everything the built-in plugin settings
/// pipeline already knows, so the callback can add sources that follow the same conventions (settings
/// directory, environment overlay naming, load-exception handling) without re-deriving them.
/// </summary>
public sealed class PluginConfigurationSourceContext
{
    /// <summary>
    /// The configuration builder the callback should append its providers to.
    /// </summary>
    public required IConfigurationBuilder Builder { get; init; }

    /// <summary>
    /// The <see cref="IFileProvider"/> scoped to the resolved plugin settings directory, i.e. the same
    /// provider the default plugin settings JSON file(s) are loaded through. <see langword="null"/> if
    /// <see cref="PluginSystemOptions.PluginSettingsFilePath"/> is not configured.
    /// </summary>
    public required IFileProvider? SettingsFileProvider { get; init; }

    /// <summary>
    /// The host environment name (e.g. "Development", "Production").
    /// </summary>
    public required string EnvironmentName { get; init; }

    /// <summary>
    /// The file name (without directory) of the default plugin settings file, e.g. "pluginsettings.json".
    /// <see langword="null"/> if <see cref="PluginSystemOptions.PluginSettingsFilePath"/> is not configured.
    /// </summary>
    public required string? SettingsFileName { get; init; }

    /// <summary>
    /// The shared load-exception handler the default plugin settings sources use: it ignores the failure
    /// and logs a warning so a malformed or half-written file neither crashes host startup nor silently
    /// wipes values on reload. Assign it to a custom <see cref="FileConfigurationSource.OnLoadException"/>
    /// to get the same behavior.
    /// </summary>
    public required Action<FileLoadExceptionContext> OnLoadException { get; init; }

    /// <summary>
    /// The host's <see cref="IServiceProvider"/>. It is fully built by the time this context is created,
    /// so any host-registered service can be resolved from it here — as long as that service does not
    /// itself depend on <see cref="IPluginSystemHostContext"/>, which would be circular.
    /// </summary>
    public required IServiceProvider HostServices { get; init; }
}
