// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Forwards every host-registered <see cref="IPluginOptionsCustomizer{TOptions}"/> into each plugin container.
/// </summary>
/// <typeparam name="TOptions">The options type the forwarded customizers adjust.</typeparam>
/// <remarks>
/// Registered by
/// <see cref="PluginOptionsCustomizerForwarderExtensions.AddPluginOptionsCustomizer{TOptions, TCustomizer}(IServiceCollection)"/>;
/// there is no reason to register it by hand.
///
/// Unlike <see cref="HostServiceForwarder{T}"/> this forwards the whole collection rather than a single
/// instance, because several customizers for one options type are the normal case - each contributes the
/// part of the options it owns.
/// </remarks>
public sealed class PluginOptionsCustomizerForwarder<TOptions>(IEnumerable<IPluginOptionsCustomizer<TOptions>> customizers)
    : IHostServiceForwarder
    where TOptions : class
{
    // Materialized once: Forward runs per plugin container, and the host registration order is the
    // order ApplyPluginOptionsCustomizers relies on.
    private readonly List<IPluginOptionsCustomizer<TOptions>> _customizers = [.. customizers];

    /// <inheritdoc />
    public void Forward(IServiceCollection pluginServices)
    {
        ArgumentNullException.ThrowIfNull(pluginServices);

        foreach (var customizer in _customizers)
        {
            pluginServices.AddSingleton(customizer);
        }
    }
}
