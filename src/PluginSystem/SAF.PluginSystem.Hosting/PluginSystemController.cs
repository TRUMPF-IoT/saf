// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <inheritdoc />
internal sealed class PluginSystemController(
    ILogger<PluginSystemController> logger,
    IPluginServicesContainer pluginServicesContainer) : IPluginSystemController
{
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Reloading plugin system.");

        await StopServicePluginsAsync(cancellationToken).ConfigureAwait(false);
        await pluginServicesContainer.ReinitializeAsync(cancellationToken).ConfigureAwait(false);
        await StartServicePluginsAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Plugin system reloaded.");
    }

    private async Task StartServicePluginsAsync(CancellationToken cancellationToken)
    {
        foreach (IServicePlugin servicePlugin in GetServicePlugins())
        {
            try
            {
                await servicePlugin.StartAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start service plug-in during reload.");
            }
        }
    }

    private async Task StopServicePluginsAsync(CancellationToken cancellationToken)
    {
        foreach (IServicePlugin servicePlugin in GetServicePlugins())
        {
            try
            {
                await servicePlugin.StopAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to stop service plug-in during reload.");
            }
        }
    }

    private List<IServicePlugin> GetServicePlugins()
        => pluginServicesContainer.GetPluginServices().SelectMany(sp => sp.GetServices<IServicePlugin>()).ToList();
}
