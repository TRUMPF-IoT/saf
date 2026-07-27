// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;

using SAF.PluginSystem.Hosting.Contracts;

/// <summary>
/// Convenience builder for a SAF host application. Provides SAF-specific configuration
/// on top of the underlying plugin system without exposing plugin system internals.
/// </summary>
public interface ISafHostBuilder
{
    /// <summary>
    /// Configures <see cref="SAF.Common.IServiceHostInfo"/> options.
    /// </summary>
    /// <param name="configure">Callback to configure the service host options.</param>
    /// <returns>The same builder for chaining.</returns>
    ISafHostBuilder ConfigureHostInfo(Action<ServiceHostOptions> configure);

    /// <summary>
    /// Configures the plugin system by delegating to the underlying <see cref="IPluginSystemHostBuilder"/>.
    /// </summary>
    /// <param name="configure">Callback receiving the plugin system host builder.</param>
    /// <returns>The same builder for chaining.</returns>
    ISafHostBuilder ConfigurePluginSystem(Action<IPluginSystemHostBuilder> configure);

    /// <summary>
    /// Adds host-level SAF diagnostics that write node info to disk on startup.
    /// </summary>
    /// <returns>The same builder for chaining.</returns>
    ISafHostBuilder AddHostDiagnostics();
}
