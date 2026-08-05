// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

/// <inheritdoc />
internal sealed class PluginSystemController(
    ILogger<PluginSystemController> logger,
    IPluginServicesContainer pluginServicesContainer,
    IPluginServicesReloader pluginServicesReloader,
    IServicePluginLifecycleRunner lifecycleRunner) : IPluginSystemController
{
    private readonly SemaphoreSlim _reloadSync = new(1, 1);

    [SuppressMessage(
        "CodeQuality",
        "S6667:Logging in a catch clause should pass the caught exception as a parameter",
        Justification = "Controller-level operational logs are intentionally kept even when exception propagation behavior is handled separately.")]
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (!pluginServicesContainer.IsInitialized)
            throw new InvalidOperationException("The plugin system has not been initialized yet. ReloadAsync can only be called after the plugin system has been started.");

        await _reloadSync.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            logger.LogInformation("Reloading plugin system.");

            // Stopping the old instances is best-effort: they and their service providers are discarded
            // anyway, so a failing plug-in is logged and the reload continues.
            List<IServicePlugin> servicePlugins = lifecycleRunner.GetServicePlugins();
            await lifecycleRunner.StoppingAsync(servicePlugins, cancellationToken).ConfigureAwait(false);
            await lifecycleRunner.StopAsync(servicePlugins, cancellationToken).ConfigureAwait(false);
            await lifecycleRunner.StoppedAsync(servicePlugins, cancellationToken).ConfigureAwait(false);

            await pluginServicesReloader.ReinitializeAsync(cancellationToken).ConfigureAwait(false);

            List<IServicePlugin> reloadedServicePlugins = lifecycleRunner.GetServicePlugins();
            await lifecycleRunner.StartingAsync(reloadedServicePlugins, cancellationToken).ConfigureAwait(false);
            await lifecycleRunner.StartAsync(reloadedServicePlugins, cancellationToken).ConfigureAwait(false);
            await lifecycleRunner.StartedAsync(reloadedServicePlugins, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Plugin system reloaded.");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Plugin system reload was canceled. The service plug-ins are left in the state the reload reached.");
            throw;
        }
        catch (Exception)
        {
            logger.LogError("Plugin system reload failed. The service plug-ins are left in the state the reload reached.");
            throw;
        }
        finally
        {
            _reloadSync.Release();
        }
    }
}
