// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Contracts;

/// <summary>
/// Represents a plugin service that participates in the host application's start/stop lifecycle.
/// Plugins that need to perform startup/shutdown work (e.g. opening connections, starting background loops)
/// implement this interface and register it via the extension method <c>AddServicePlugin</c>.
/// Similar in purpose to <see cref="Microsoft.Extensions.Hosting.IHostedService"/>, but scoped
/// to the plugin system.
/// </summary>
public interface IServicePlugin
{
    /// <summary>
    /// Starts the plugin service asynchronously.
    /// </summary>
    /// <param name="token">A <see cref="CancellationToken"/> that signals when the start should be aborted.</param>
    /// <returns>A task representing the asynchronous start operation.</returns>
    Task StartAsync(CancellationToken token);

    /// <summary>
    /// Stops the plugin service asynchronously.
    /// </summary>
    /// <param name="token">A <see cref="CancellationToken"/> that signals when the stop should be aborted.</param>
    /// <returns>A task representing the asynchronous stop operation.</returns>
    Task StopAsync(CancellationToken token);
}