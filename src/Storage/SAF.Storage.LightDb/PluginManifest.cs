// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Storage.LiteDb;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SAF.PluginSystem.Hosting.Contracts;

public class PluginManifest : IPluginManifest
{
    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
    {
        pluginServices.AddLiteDbStorageInfrastructure(config =>
        {
            config.ConnectionString =
                context.HostConfiguration["LiteDb:ConnectionString"]
                ?? context.HostConfiguration["LiteDbConfiguration:ConnectionString"]
                ?? string.Empty;
        });
    }
}
