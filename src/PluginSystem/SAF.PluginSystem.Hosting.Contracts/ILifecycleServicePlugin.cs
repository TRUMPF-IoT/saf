// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace SAF.PluginSystem.Hosting.Contracts;

/// <summary>
/// Extends <see cref="IServicePlugin"/> with finer-grained lifecycle hooks for plugins
/// that need phased initialization and shutdown (starting, started, stopping, stopped).
/// Similar in purpose to <see cref="Microsoft.Extensions.Hosting.IHostedLifecycleService"/>,
/// but scoped to the plugin system.
/// </summary>
public interface ILifecycleServicePlugin : IServicePlugin
{
    /// <summary>
    /// Called before <see cref="IServicePlugin.StartAsync"/> to perform pre-start initialization.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that signals cancellation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Called after <see cref="IServicePlugin.StartAsync"/> has completed to perform post-start operations.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that signals cancellation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Called before <see cref="IServicePlugin.StopAsync"/> to perform pre-stop cleanup.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that signals cancellation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StoppingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Called after <see cref="IServicePlugin.StopAsync"/> has completed to perform post-stop cleanup.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that signals cancellation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StoppedAsync(CancellationToken cancellationToken);
}