// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Microsoft.Extensions.DependencyInjection;
using SAF.PluginSystem.Hosting.Contracts;

/// <summary>
/// Forwards a resolved host service instance of type <typeparamref name="T"/> into each plugin container.
/// </summary>
/// <typeparam name="T">The service type to forward. Must be a reference type.</typeparam>
/// <remarks>
/// Register via <see cref="HostServiceForwarderExtensions.AddHostServiceForwarder{T}(IServiceCollection)"/>
/// after registering <typeparamref name="T"/> in the host container. Do not also register this type by
/// hand (<c>services.AddSingleton&lt;IHostServiceForwarder, HostServiceForwarder&lt;T&gt;&gt;()</c>) for a
/// <typeparamref name="T"/> that <c>AddHostServiceForwarder{T}</c> already forwards: <c>TryAddEnumerable</c>
/// only stops itself from adding a second copy, it does not retroactively deduplicate against a plain
/// <c>Add*</c> call made afterwards, so <see cref="Forward"/> would then run twice per plugin.
/// </remarks>
public sealed class HostServiceForwarder<T>(T service) : IHostServiceForwarder
    where T : class
{
    /// <inheritdoc />
    public void Forward(IServiceCollection pluginServices)
        => pluginServices.AddSingleton(service);
}
