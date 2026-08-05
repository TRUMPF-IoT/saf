// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Contracts;

/// <summary>
/// Provides access to all plugin dependency injection (DI) containers managed by the plugin system.
/// </summary>
public interface IPluginServicesContainer
{
    /// <summary>
    /// Gets a value indicating whether the container has been initialized at least once.
    /// </summary>
    /// <remarks>
    /// This is <see langword="false" /> until either <see cref="GetPluginServices" />
    /// or <see cref="GetPublicServices" /> has been called for the first time.
    /// </remarks>
    bool IsInitialized { get; }

    /// <summary>
    /// Gets the <see cref="IServiceProvider"/> instances for all loaded plugins.
    /// </summary>
    /// <returns>An enumerable of <see cref="IServiceProvider"/> instances, one per plugin.</returns>
    IEnumerable<IServiceProvider> GetPluginServices();

    /// <summary>
    /// Gets a service provider instance that supplies public services which are available to all plugins used for cross-plugin communication.
    /// </summary>
    /// <remarks>Use this service provider to access common infrastructure or cross-plugin services.</remarks>
    /// <returns>An <see cref="IServiceProvider"/> that provides access to public services for plugins. The returned instance may
    /// be reused across multiple plugin invocations.</returns>
    IServiceProvider GetPublicServices();
}
