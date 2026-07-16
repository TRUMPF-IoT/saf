// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Contracts;

/// <summary>
/// Coordinates host-level lifecycle operations of the plugin system that span all loaded plugins.
/// </summary>
/// <remarks>
/// Registered by <c>AddPluginSystem</c>. Use it to trigger a live reload of the plugin service
/// providers from the current configuration without restarting the host process.
/// </remarks>
public interface IPluginSystemController
{
    /// <summary>
    /// Reloads the plugin system in-process: stops the running service plugins, rebuilds the plugin
    /// service providers from the current configuration via
    /// <see cref="IPluginServicesContainer.ReinitializeAsync"/>, and starts the service plugins again.
    /// </summary>
    /// <remarks>
    /// The underlying plugin assembly load contexts (ALCs) are left untouched, so no assembly is
    /// reloaded and no ALC is leaked. A plugin binary that was absent at startup is therefore not
    /// picked up by a reload and still requires a controlled restart.
    /// </remarks>
    /// <param name="cancellationToken">A token that signals cancellation of the reload.</param>
    /// <returns>A task that completes once the plugin system has been reloaded.</returns>
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
