// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;

/// <summary>
/// The <see cref="IServiceProvider"/> handed to plugin configuration source callbacks as
/// <see cref="PluginConfigurationSourceContext.HostServices"/>. It forwards to the host container, and
/// lets the <see cref="IPluginSystemHostContext"/> factory recognise the one resolution it must not
/// serve: a service whose construction needs the host context these callbacks are running inside.
/// </summary>
internal sealed class PluginConfigurationHostServices(IServiceProvider hostServices) : IServiceProvider
{
    private Type? _resolving;

    /// <summary>
    /// Whether a resolution started from a callback is in flight. Only such a resolution can reach the
    /// <see cref="IPluginSystemHostContext"/> factory while that factory is already running, so the
    /// factory uses this to tell a re-entrant call from a first one.
    /// </summary>
    public bool IsResolving => _resolving is not null;

    /// <inheritdoc />
    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        // The container resolves IServiceProvider to itself; forwarding that would hand out a way around
        // this guard in the same breath.
        if (serviceType == typeof(IServiceProvider))
        {
            return this;
        }

        var outer = _resolving;
        _resolving = serviceType;
        try
        {
            return hostServices.GetService(serviceType);
        }
        finally
        {
            _resolving = outer;
        }
    }

    /// <summary>
    /// The exception the <see cref="IPluginSystemHostContext"/> factory throws instead of letting itself
    /// be re-entered.
    /// </summary>
    // Dependency injection does not detect this cycle: it re-invokes the factory it is already inside,
    // endlessly, and the process dies of an uncatchable StackOverflowException with no log line.
    public InvalidOperationException CircularResolution() => new(
        $"'{_resolving}' cannot be resolved from {nameof(PluginConfigurationSourceContext)}."
        + $"{nameof(PluginConfigurationSourceContext.HostServices)}: constructing it needs "
        + $"{nameof(IPluginSystemHostContext)}, which is the service being built right now - the plugin "
        + "configuration these callbacks contribute to is part of it. Left to the container this ends in a "
        + "StackOverflowException, so it is refused here. Resolve only services that do not depend on the "
        + "plugin system from a configuration source callback; take the ones that do from a plugin "
        + "manifest's ConfigureServices or from a hosted service, where the host context already exists.");
}
