// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Storage.SqLite;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SAF.PluginSystem.Hosting.Contracts;

public class PluginManifest : IPluginManifest
{
    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
    {
        var sqLiteSection = context.PluginConfiguration.GetSection("SqLite");
        if (!sqLiteSection.Exists())
        {
            sqLiteSection = context.HostConfiguration.GetSection("SqLite");
        }

        var legacySqLiteSection = context.PluginConfiguration.GetSection("SqLiteConfiguration");
        if (!legacySqLiteSection.Exists())
        {
            legacySqLiteSection = context.HostConfiguration.GetSection("SqLiteConfiguration");
        }

        pluginServices.AddSqLiteStorageInfrastructure(config =>
        {
            config.ConnectionString =
                sqLiteSection["ConnectionString"]
                ?? legacySqLiteSection["ConnectionString"]
                ?? string.Empty;
        });
    }
}
