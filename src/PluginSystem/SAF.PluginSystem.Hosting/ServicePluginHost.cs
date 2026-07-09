// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace SAF.PluginSystem.Hosting;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

internal class ServicePluginHost(ILogger<ServicePluginHost> logger, IPluginServicesContainer pluginServicesContainer) : IHostedLifecycleService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Starting service plugins.");

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var servicePlugins = GetServicePlugins();
        foreach (IServicePlugin servicePlugin in servicePlugins)
        {
            try
            {
                await servicePlugin.StartAsync(linkedCts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start service plug-in.");
            }
        }

        logger.LogInformation("Service plugins started.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Stopping service plugins.");

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var servicePlugins = GetServicePlugins();

        foreach (IServicePlugin servicePlugin in servicePlugins)
        {
            try
            {
                await servicePlugin.StopAsync(linkedCts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to stop service plug-in.");
            }
        }

        logger.LogInformation("Service plugins stopped.");
    }

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var lifecyclePlugins = GetServicePlugins().OfType<ILifecycleServicePlugin>();

        foreach (var plugin in lifecyclePlugins)
        {
            await plugin.StartingAsync(linkedCts.Token).ConfigureAwait(false);
        }
    }

    public async Task StartedAsync(CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var lifecyclePlugins = GetServicePlugins().OfType<ILifecycleServicePlugin>();

        foreach (var plugin in lifecyclePlugins)
        {
            await plugin.StartedAsync(linkedCts.Token).ConfigureAwait(false);
        }
    }

    public async Task StoppingAsync(CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var lifecyclePlugins = GetServicePlugins().OfType<ILifecycleServicePlugin>();

        foreach (var plugin in lifecyclePlugins)
        {
            await plugin.StoppingAsync(linkedCts.Token).ConfigureAwait(false);
        }
    }

    public async Task StoppedAsync(CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var lifecyclePlugins = GetServicePlugins().OfType<ILifecycleServicePlugin>();

        foreach (var plugin in lifecyclePlugins)
        {
            await plugin.StoppedAsync(linkedCts.Token).ConfigureAwait(false);
        }
    }

    private List<IServicePlugin> GetServicePlugins()
        => pluginServicesContainer.GetPluginServices().SelectMany(sp => sp.GetServices<IServicePlugin>()).ToList();
}