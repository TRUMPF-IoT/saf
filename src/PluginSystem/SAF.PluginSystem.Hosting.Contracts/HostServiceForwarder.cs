// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Contracts;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Forwards a resolved host service instance of type <typeparamref name="T"/> into each plugin container.
/// </summary>
/// <typeparam name="T">The service type to forward. Must be a reference type.</typeparam>
/// <remarks>
/// Register via
/// <c>services.AddSingleton&lt;IHostServiceForwarder, HostServiceForwarder&lt;T&gt;&gt;()</c>
/// after registering <typeparamref name="T"/> in the host container.
/// </remarks>
public sealed class HostServiceForwarder<T>(T service) : IHostServiceForwarder
    where T : class
{
    /// <inheritdoc />
    public void Forward(IServiceCollection pluginServices)
        => pluginServices.AddSingleton(service);
}
