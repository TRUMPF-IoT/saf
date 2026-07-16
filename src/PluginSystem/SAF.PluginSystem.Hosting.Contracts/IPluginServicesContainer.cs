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

    /// <summary>
    /// Rebuilds the plugin dependency injection (DI) service providers from the current
    /// <see cref="IPluginSystemHostContext.PluginConfiguration"/>, without recreating the underlying
    /// plugin assembly load contexts (ALCs).
    /// </summary>
    /// <remarks>
    /// Re-runs each plugin manifest's <see cref="IPluginManifest.ConfigureServices"/> on fresh service
    /// collections and rebuilds the cross-plugin service wiring, then disposes the previously built
    /// providers. Because the loaded assemblies / ALCs owned by the plugin containers are left untouched,
    /// this provides live reconfiguration without a process restart and without leaking assembly load
    /// contexts. After this call, <see cref="GetPluginServices"/> and <see cref="GetPublicServices"/>
    /// return the freshly built providers.
    /// </remarks>
    /// <param name="cancellationToken">A token that signals cancellation of the reinitialization.</param>
    /// <returns>A <see cref="ValueTask"/> that completes once the providers have been rebuilt and the previous providers disposed.</returns>
    ValueTask ReinitializeAsync(CancellationToken cancellationToken = default);
}