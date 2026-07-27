// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Contracts;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// The single entry point every plugin must provide. Discovered via reflection during
/// assembly scanning and used to configure the plugin's dependency injection container.
/// Each assembly may contain at most one <see cref="IPluginManifest"/> implementation.
/// </summary>
/// <remarks>
/// Every plugin receives its own isolated <see cref="IServiceCollection"/> and, after
/// configuration, its own <see cref="IServiceProvider"/>. Services registered against
/// public interfaces can be resolved by other plugins through <see cref="IPluginServiceProvider"/>,
/// which queries all containers. Services registered against non-public types are effectively
/// private because other plugins cannot reference those types.
/// Common host services (logging, configuration, etc.) are available in every plugin
/// container, but plugins cannot directly access each other's <see cref="IServiceProvider"/>.
/// </remarks>
public interface IPluginManifest
{
    /// <summary>
    /// Configures the plugin's services by registering them into the provided <paramref name="pluginServices"/> collection.
    /// </summary>
    /// <param name="context">The <see cref="IPluginSystemHostContext"/> providing access to host and plugin configuration.</param>
    /// <param name="pluginServices">The <see cref="IServiceCollection"/> to register plugin services into.</param>
    void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices);
}