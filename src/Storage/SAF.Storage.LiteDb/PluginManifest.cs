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
        var liteDbSection = context.PluginConfiguration.GetSection("LiteDb");
        if (!liteDbSection.Exists())
        {
            liteDbSection = context.HostConfiguration.GetSection("LiteDb");
        }

        var legacyLiteDbSection = context.PluginConfiguration.GetSection("LiteDbConfiguration");
        if (!legacyLiteDbSection.Exists())
        {
            legacyLiteDbSection = context.HostConfiguration.GetSection("LiteDbConfiguration");
        }

        pluginServices.AddLiteDbStorageInfrastructure(config =>
        {
            config.ConnectionString =
                liteDbSection["ConnectionString"]
                ?? legacyLiteDbSection["ConnectionString"]
                ?? string.Empty;
        });
    }
}
