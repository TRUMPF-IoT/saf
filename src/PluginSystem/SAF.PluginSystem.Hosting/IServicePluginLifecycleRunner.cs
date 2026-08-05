// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;

/// <summary>
/// Executes the full start/stop lifecycle (including all <see cref="ILifecycleServicePlugin"/> phases)
/// for a set of <see cref="IServicePlugin"/> instances.
/// </summary>
/// <remarks>
/// All operations are best-effort: a failure of a single plug-in is logged and the run continues with the
/// remaining plug-ins. Only cancellation aborts a run, and is reported as
/// <see cref="OperationCanceledException"/>.
/// </remarks>
internal interface IServicePluginLifecycleRunner
{
    /// <summary>
    /// Resolves all <see cref="IServicePlugin"/> instances from the current plugin service providers.
    /// </summary>
    List<IServicePlugin> GetServicePlugins();

    /// <summary>
    /// Executes the <c>StartingAsync</c> lifecycle phase on all <see cref="ILifecycleServicePlugin"/> instances.
    /// </summary>
    Task StartingAsync(IEnumerable<IServicePlugin> servicePlugins, CancellationToken cancellationToken);

    /// <summary>
    /// Starts all <paramref name="servicePlugins"/> via <see cref="IServicePlugin.StartAsync"/>.
    /// </summary>
    Task StartAsync(IEnumerable<IServicePlugin> servicePlugins, CancellationToken cancellationToken);

    /// <summary>
    /// Executes the <c>StartedAsync</c> lifecycle phase on all <see cref="ILifecycleServicePlugin"/> instances.
    /// </summary>
    Task StartedAsync(IEnumerable<IServicePlugin> servicePlugins, CancellationToken cancellationToken);

    /// <summary>
    /// Executes the <c>StoppingAsync</c> lifecycle phase on all <see cref="ILifecycleServicePlugin"/> instances.
    /// </summary>
    Task StoppingAsync(IEnumerable<IServicePlugin> servicePlugins, CancellationToken cancellationToken);

    /// <summary>
    /// Stops all <paramref name="servicePlugins"/> via <see cref="IServicePlugin.StopAsync"/>.
    /// </summary>
    Task StopAsync(IEnumerable<IServicePlugin> servicePlugins, CancellationToken cancellationToken);

    /// <summary>
    /// Executes the <c>StoppedAsync</c> lifecycle phase on all <see cref="ILifecycleServicePlugin"/> instances.
    /// </summary>
    Task StoppedAsync(IEnumerable<IServicePlugin> servicePlugins, CancellationToken cancellationToken);
}
