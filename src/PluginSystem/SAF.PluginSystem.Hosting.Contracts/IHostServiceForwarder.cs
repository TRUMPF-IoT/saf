// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Contracts;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Forwards a host-level service into a plugin's isolated service collection.
/// Register implementations in the host's <see cref="IServiceCollection"/> to bridge host→plugin DI
/// without exposing the host <see cref="IServiceProvider"/> (Service Locator anti-pattern).
/// </summary>
/// <remarks>
/// <see cref="IHostServiceForwarder"/> instances registered in the host container are resolved and
/// invoked by the plugin system before each plugin manifest's <c>ConfigureServices</c> runs.
/// This keeps forwarding decisions in the host layer, not in individual plugin manifests.
///
/// Forward host-owned singletons as concrete instances (for example with <c>AddSingleton(instance)</c>
/// or <see cref="HostServiceForwarder{T}"/>) instead of factory delegates that return host instances,
/// so plugin containers do not take ownership of host lifetimes.
/// </remarks>
public interface IHostServiceForwarder
{
    /// <summary>
    /// Registers the forwarded service into <paramref name="pluginServices"/>.
    /// </summary>
    /// <param name="pluginServices">The plugin's isolated <see cref="IServiceCollection"/>.</param>
    void Forward(IServiceCollection pluginServices);
}
