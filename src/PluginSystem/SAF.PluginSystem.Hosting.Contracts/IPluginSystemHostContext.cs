// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Contracts;

using Microsoft.Extensions.Configuration;

/// <summary>
/// Provides the host context available to plugins during service configuration.
/// Grants access to the host environment, host-wide configuration, and the plugin-specific
/// configuration section.
/// </summary>
public interface IPluginSystemHostContext
{
    /// <summary>
    /// Gets the <see cref="IPluginSystemHostEnvironment"/> providing runtime context
    /// such as the plugin settings root path and environment name.
    /// </summary>
    IPluginSystemHostEnvironment Environment { get; }

    /// <summary>
    /// Gets the host-wide <see cref="IConfiguration"/> (e.g. from <c>appsettings.json</c>).
    /// </summary>
    IConfiguration HostConfiguration { get; }

    /// <summary>
    /// Gets the plugin-specific <see cref="IConfiguration"/> (e.g. from <c>pluginsettings.json</c>).
    /// </summary>
    IConfiguration PluginConfiguration { get; }
}