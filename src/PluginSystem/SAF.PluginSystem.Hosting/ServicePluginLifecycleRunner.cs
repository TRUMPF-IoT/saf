// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.ExceptionServices;

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
        => ExecuteUntilCanceledAsync(
            servicePlugins.OfType<ILifecycleServicePlugin>(),
            static (plugin, ct) => plugin.StartingAsync(ct),
            LogPhaseFailure(nameof(ILifecycleServicePlugin.StartingAsync)),
            cancellationToken);

    /// <inheritdoc />
    public Task StartAsync(IEnumerable<IServicePlugin> servicePlugins, CancellationToken cancellationToken)
        => ExecuteUntilCanceledAsync(
            servicePlugins,
            static (plugin, ct) => plugin.StartAsync(ct),
            ex => logger.LogError(ex, "Failed to start service plug-in."),
            cancellationToken);

    /// <inheritdoc />
    public Task StartedAsync(IEnumerable<IServicePlugin> servicePlugins, CancellationToken cancellationToken)
        => ExecuteUntilCanceledAsync(
            servicePlugins.OfType<ILifecycleServicePlugin>(),
            static (plugin, ct) => plugin.StartedAsync(ct),
            LogPhaseFailure(nameof(ILifecycleServicePlugin.StartedAsync)),
            cancellationToken);

    /// <inheritdoc />
    public Task StoppingAsync(IEnumerable<IServicePlugin> servicePlugins, CancellationToken cancellationToken)
        => ExecuteAllAsync(
            InStopOrder(servicePlugins).OfType<ILifecycleServicePlugin>(),
            static (plugin, ct) => plugin.StoppingAsync(ct),
            LogPhaseFailure(nameof(ILifecycleServicePlugin.StoppingAsync)),
            cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(IEnumerable<IServicePlugin> servicePlugins, CancellationToken cancellationToken)
        => ExecuteAllAsync(
            InStopOrder(servicePlugins),
            static (plugin, ct) => plugin.StopAsync(ct),
            ex => logger.LogError(ex, "Failed to stop service plug-in."),
            cancellationToken);

    /// <inheritdoc />
    public Task StoppedAsync(IEnumerable<IServicePlugin> servicePlugins, CancellationToken cancellationToken)
        => ExecuteAllAsync(
            InStopOrder(servicePlugins).OfType<ILifecycleServicePlugin>(),
            static (plugin, ct) => plugin.StoppedAsync(ct),
            LogPhaseFailure(nameof(ILifecycleServicePlugin.StoppedAsync)),
            cancellationToken);

    // Like the .NET host with its hosted services, the plug-ins stop in the reverse order they start in: a plug-in
    // commonly uses the services of the plug-ins started before it, such as the messaging, and can then still use
    // them while it stops.
    private static IEnumerable<IServicePlugin> InStopOrder(IEnumerable<IServicePlugin> servicePlugins)
        => servicePlugins.Reverse();

    private Action<Exception> LogPhaseFailure(string phaseName)
        => ex => logger.LogError(ex, "Failed to execute {PhaseName} for service plug-in.", phaseName);

    private static async Task ExecuteUntilCanceledAsync<TPlugin>(
        IEnumerable<TPlugin> servicePlugins,
        Func<TPlugin, CancellationToken, Task> execute,
        Action<Exception> logFailure,
        CancellationToken cancellationToken)
    {
        // Scopes the token to this phase, so callbacks a plug-in leaves registered do not stay on the caller's token.
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

    // Like the .NET host, still runs the remaining plug-ins after a cancellation and reports it afterwards.
    private static async Task ExecuteAllAsync<TPlugin>(
        IEnumerable<TPlugin> servicePlugins,
        Func<TPlugin, CancellationToken, Task> execute,
        Action<Exception> logFailure,
        CancellationToken cancellationToken)
    {
        // Scopes the token to this phase, so callbacks a plug-in leaves registered do not stay on the caller's token.
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        OperationCanceledException? cancellation = null;

        foreach (TPlugin servicePlugin in servicePlugins)
        {
            try
            {
                await execute(servicePlugin, linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                cancellation ??= ex;
            }
            catch (Exception ex)
            {
                logFailure(ex);
            }
        }

        if (cancellation is not null)
        {
            ExceptionDispatchInfo.Throw(cancellation);
        }
    }
}
