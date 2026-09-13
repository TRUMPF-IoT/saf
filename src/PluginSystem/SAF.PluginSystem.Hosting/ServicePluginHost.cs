// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using AssemblyLoading;
using Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

internal class ServicePluginHost(ILogger<ServicePluginHost> logger, IServicePluginLifecycleRunner runner, ISharedAssemblyRegistry sharedAssemblyRegistry) : IHostedLifecycleService
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
    {
        // Computes the shared assembly set here, before the first plugin gets a chance to load, instead of
        // leaving it to build lazily inside the CLR's assembly-bind callback for whichever plugin binds
        // first - which would pay for the full initialization there and blame a misbehaving shared assembly
        // source's failure on an innocent plugin.
        sharedAssemblyRegistry.GetSharedAssemblies();
        return runner.StartingAsync(runner.GetServicePlugins(), cancellationToken);
    }

    public Task StartedAsync(CancellationToken cancellationToken)
        => runner.StartedAsync(runner.GetServicePlugins(), cancellationToken);

    public Task StoppingAsync(CancellationToken cancellationToken)
        => runner.StoppingAsync(runner.GetServicePlugins(), cancellationToken);

    public Task StoppedAsync(CancellationToken cancellationToken)
        => runner.StoppedAsync(runner.GetServicePlugins(), cancellationToken);
}