// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SAF.Common;
using SAF.PluginSystem.Hosting.Contracts;

/// <summary>
/// Bridges <see cref="IServiceHostInfo"/> into the plugin system without changing the plugin system implementation.
/// </summary>
public sealed class PluginManifest : IPluginManifest
{
    private const string ServiceHostSectionName = "ServiceHost";

    /// <inheritdoc />
    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(pluginServices);

        var options = new ServiceHostOptions();
        context.HostConfiguration.GetSection(ServiceHostSectionName).Bind(options);

        pluginServices.AddSingleton<IServiceHostInfo>(_ =>
            new ServiceHostInfo(options, static () => Guid.NewGuid().ToString("N")));
    }
}
