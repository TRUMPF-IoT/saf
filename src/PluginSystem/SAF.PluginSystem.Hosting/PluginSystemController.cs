// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using System.Diagnostics.CodeAnalysis;
using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <inheritdoc />
internal sealed class PluginSystemController(
    ILogger<PluginSystemController> logger,
    IPluginServicesContainer pluginServicesContainer) : IPluginSystemController
{
    private readonly SemaphoreSlim _reloadSync= new(1, 1);

    [SuppressMessage(
        "CodeQuality",
        "S6667:Logging in a catch clause should pass the caught exception as a parameter",
        Justification = "Controller-level operational logs are intentionally kept even when exception propagation behavior is handled separately.")]
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (!pluginServicesContainer.IsInitialized)
            throw new InvalidOperationException("The plugin system has not been initialized yet. ReloadAsync can only be called after the plugin system has been started.");

        await _reloadSync.WaitAsync(cancellationToken).ConfigureAwait(false);

        List<IServicePlugin> stoppedServicePlugins = [];

        try
        {
            logger.LogInformation("Reloading plugin system.");

            List<IServicePlugin> servicePlugins = GetServicePlugins();
            stoppedServicePlugins = await StopServicePluginsAsync(servicePlugins, cancellationToken).ConfigureAwait(false);

            await pluginServicesContainer.ReinitializeAsync(cancellationToken).ConfigureAwait(false);

            await StartServicePluginsAsync(GetServicePlugins(), cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation("Plugin system reloaded.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Plugin system reload was canceled. Attempting to restart previously stopped service plug-ins.");
            await StartServicePluginsAsync(stoppedServicePlugins, CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
        catch (Exception)
        {
            logger.LogError("Plugin system reload failed. Attempting to restart previously stopped service plug-ins.");
            await StartServicePluginsAsync(stoppedServicePlugins, CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
        finally
        {
            _reloadSync.Release();
        }
    }

    private async Task StartServicePluginsAsync(
        IEnumerable<IServicePlugin> servicePlugins,
        CancellationToken cancellationToken)
    {
        List<IServicePlugin> servicePluginsList = [.. servicePlugins];

        await ExecuteLifecyclePhaseAsync(
            servicePluginsList,
            static (plugin, ct) => plugin.StartingAsync(ct),
            "StartingAsync",
            cancellationToken).ConfigureAwait(false);

        using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            foreach (IServicePlugin servicePlugin in servicePluginsList)
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
                    logger.LogError(ex, "Failed to start service plug-in during reload.");
                }
            }
        }

        await ExecuteLifecyclePhaseAsync(
            servicePluginsList,
            static (plugin, ct) => plugin.StartedAsync(ct),
            "StartedAsync",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<IServicePlugin>> StopServicePluginsAsync(
        IEnumerable<IServicePlugin> servicePlugins,
        CancellationToken cancellationToken)
    {
        List<IServicePlugin> servicePluginsList = [.. servicePlugins];
        List<IServicePlugin> stoppedServicePlugins = [];

        await ExecuteLifecyclePhaseAsync(
            servicePluginsList,
            static (plugin, ct) => plugin.StoppingAsync(ct),
            "StoppingAsync",
            cancellationToken).ConfigureAwait(false);

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
                    logger.LogError(ex, "Failed to stop service plug-in during reload.");
                }
            }
        }

        await ExecuteLifecyclePhaseAsync(
            servicePluginsList,
            static (plugin, ct) => plugin.StoppedAsync(ct),
            "StoppedAsync",
            cancellationToken).ConfigureAwait(false);

        return stoppedServicePlugins;
    }

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
                logger.LogError(ex, "Failed to execute {PhaseName} for service plug-in during reload.", phaseName);
            }
        }
    }

    private List<IServicePlugin> GetServicePlugins()
        => [.. pluginServicesContainer.GetPluginServices().SelectMany(sp => sp.GetServices<IServicePlugin>())];
}
