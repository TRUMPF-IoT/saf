// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

internal class ServicePluginHost(ILogger<ServicePluginHost> logger, IServicePluginLifecycleRunner runner) : IHostedLifecycleService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Starting service plugins.");
        await runner.StartAsync(runner.GetServicePlugins(), cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Service plugins started.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Stopping service plugins.");
        await runner.StopAsync(runner.GetServicePlugins(), cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Service plugins stopped.");
    }

    public Task StartingAsync(CancellationToken cancellationToken)
        => runner.StartingAsync(runner.GetServicePlugins(), cancellationToken);

    public Task StartedAsync(CancellationToken cancellationToken)
        => runner.StartedAsync(runner.GetServicePlugins(), cancellationToken);

    public Task StoppingAsync(CancellationToken cancellationToken)
        => runner.StoppingAsync(runner.GetServicePlugins(), cancellationToken);

    public Task StoppedAsync(CancellationToken cancellationToken)
        => runner.StoppedAsync(runner.GetServicePlugins(), cancellationToken);
}