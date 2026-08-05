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
    public async Task StartAsync(IEnumerable<IServicePlugin> servicePlugins, CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        foreach (IServicePlugin servicePlugin in servicePlugins)
        {
            try
            {
                await servicePlugin.StartAsync(linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start service plug-in.");
            }
        }
    }

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
    public async Task<List<IServicePlugin>> StopAsync(IEnumerable<IServicePlugin> servicePlugins, CancellationToken cancellationToken)
    {
        List<IServicePlugin> servicePluginsList = [.. servicePlugins];
        List<IServicePlugin> stoppedServicePlugins = [];

        using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            foreach (IServicePlugin servicePlugin in servicePluginsList)
            {
                try
                {
                    await servicePlugin.StopAsync(linkedCts.Token).ConfigureAwait(false);
                    stoppedServicePlugins.Add(servicePlugin);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to stop service plug-in.");
                }
            }
        }

        return stoppedServicePlugins;
    }

    /// <inheritdoc />
    public Task StoppedAsync(IEnumerable<IServicePlugin> servicePlugins, CancellationToken cancellationToken)
        => ExecuteLifecyclePhaseAsync(
            servicePlugins,
            static (plugin, ct) => plugin.StoppedAsync(ct),
            nameof(ILifecycleServicePlugin.StoppedAsync),
            cancellationToken);

    private async Task ExecuteLifecyclePhaseAsync(
        IEnumerable<IServicePlugin> servicePlugins,
        Func<ILifecycleServicePlugin, CancellationToken, Task> executePhase,
        string phaseName,
        CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        foreach (ILifecycleServicePlugin servicePlugin in servicePlugins.OfType<ILifecycleServicePlugin>())
        {
            try
            {
                await executePhase(servicePlugin, linkedCts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to execute {PhaseName} for service plug-in.", phaseName);
            }
        }
    }
}
