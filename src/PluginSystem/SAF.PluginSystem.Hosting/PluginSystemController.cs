// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using System.Diagnostics.CodeAnalysis;
using Contracts;
using Microsoft.Extensions.Logging;

/// <inheritdoc />
internal sealed class PluginSystemController(
    ILogger<PluginSystemController> logger,
    IPluginServicesContainer pluginServicesContainer,
    IServicePluginLifecycleRunner lifecycleRunner) : IPluginSystemController
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

            List<IServicePlugin> servicePlugins = lifecycleRunner.GetServicePlugins();

            await lifecycleRunner.StoppingAsync(servicePlugins, cancellationToken).ConfigureAwait(false);
            stoppedServicePlugins = await lifecycleRunner.StopAsync(servicePlugins, cancellationToken).ConfigureAwait(false);
            await lifecycleRunner.StoppedAsync(servicePlugins, cancellationToken).ConfigureAwait(false);

            await pluginServicesContainer.ReinitializeAsync(cancellationToken).ConfigureAwait(false);

            List<IServicePlugin> newServicePlugins = lifecycleRunner.GetServicePlugins();
            await lifecycleRunner.StartingAsync(newServicePlugins, cancellationToken).ConfigureAwait(false);
            await lifecycleRunner.StartAsync(newServicePlugins, cancellationToken).ConfigureAwait(false);
            await lifecycleRunner.StartedAsync(newServicePlugins, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Plugin system reloaded.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Plugin system reload was canceled. Attempting to restart previously stopped service plug-ins.");
            await lifecycleRunner.StartingAsync(stoppedServicePlugins, CancellationToken.None).ConfigureAwait(false);
            await lifecycleRunner.StartAsync(stoppedServicePlugins, CancellationToken.None).ConfigureAwait(false);
            await lifecycleRunner.StartedAsync(stoppedServicePlugins, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception)
        {
            logger.LogError("Plugin system reload failed. Attempting to restart previously stopped service plug-ins.");
            await lifecycleRunner.StartingAsync(stoppedServicePlugins, CancellationToken.None).ConfigureAwait(false);
            await lifecycleRunner.StartAsync(stoppedServicePlugins, CancellationToken.None).ConfigureAwait(false);
            await lifecycleRunner.StartedAsync(stoppedServicePlugins, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            _reloadSync.Release();
        }
    }
}

