// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <inheritdoc />
internal sealed class ServicePluginLifecycleRunner(
    ILogger<ServicePluginLifecycleRunner> logger,
    IPluginServicesContainer pluginServicesContainer) : IServicePluginLifecycleRunner
{
    /// <inheritdoc />
    public List<IServicePlugin> GetServicePlugins()
        => [.. pluginServicesContainer.GetPluginServices().SelectMany(sp => sp.GetServices<IServicePlugin>())];

    /// <inheritdoc />
    public Task StartingAsync(IEnumerable<IServicePlugin> servicePlugins, CancellationToken cancellationToken)
        => ExecuteLifecyclePhaseAsync(
            servicePlugins,
            static (plugin, ct) => plugin.StartingAsync(ct),
            nameof(ILifecycleServicePlugin.StartingAsync),
            cancellationToken);

    /// <inheritdoc />
    public Task StartAsync(IEnumerable<IServicePlugin> servicePlugins, CancellationToken cancellationToken)
        => ExecuteAsync(
            servicePlugins,
            static (plugin, ct) => plugin.StartAsync(ct),
            ex => logger.LogError(ex, "Failed to start service plug-in."),
            cancellationToken);

    /// <inheritdoc />
    public Task StartedAsync(IEnumerable<IServicePlugin> servicePlugins, CancellationToken cancellationToken)
        => ExecuteLifecyclePhaseAsync(
            servicePlugins,
            static (plugin, ct) => plugin.StartedAsync(ct),
            nameof(ILifecycleServicePlugin.StartedAsync),
            cancellationToken);

    /// <inheritdoc />
    public Task StoppingAsync(IEnumerable<IServicePlugin> servicePlugins, CancellationToken cancellationToken)
        => ExecuteLifecyclePhaseAsync(
            servicePlugins,
            static (plugin, ct) => plugin.StoppingAsync(ct),
            nameof(ILifecycleServicePlugin.StoppingAsync),
            cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(IEnumerable<IServicePlugin> servicePlugins, CancellationToken cancellationToken)
        => ExecuteAsync(
            servicePlugins,
            static (plugin, ct) => plugin.StopAsync(ct),
            ex => logger.LogError(ex, "Failed to stop service plug-in."),
            cancellationToken);

    /// <inheritdoc />
    public Task StoppedAsync(IEnumerable<IServicePlugin> servicePlugins, CancellationToken cancellationToken)
        => ExecuteLifecyclePhaseAsync(
            servicePlugins,
            static (plugin, ct) => plugin.StoppedAsync(ct),
            nameof(ILifecycleServicePlugin.StoppedAsync),
            cancellationToken);

    private Task ExecuteLifecyclePhaseAsync(
        IEnumerable<IServicePlugin> servicePlugins,
        Func<ILifecycleServicePlugin, CancellationToken, Task> executePhase,
        string phaseName,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            servicePlugins.OfType<ILifecycleServicePlugin>(),
            executePhase,
            ex => logger.LogError(ex, "Failed to execute {PhaseName} for service plug-in.", phaseName),
            cancellationToken);

    private static async Task ExecuteAsync<TPlugin>(
        IEnumerable<TPlugin> servicePlugins,
        Func<TPlugin, CancellationToken, Task> execute,
        Action<Exception> logFailure,
        CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        foreach (TPlugin servicePlugin in servicePlugins)
        {
            try
            {
                await execute(servicePlugin, linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logFailure(ex);
            }
        }
    }
}
