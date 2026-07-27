// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Storage.SQLite;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SAF.PluginSystem.Hosting.Contracts;

public class PluginManifest : IPluginManifest
{
    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
    {
        var sqliteSection = context.PluginConfiguration.GetSection("SQLite");
        if (!sqliteSection.Exists())
        {
            sqliteSection = context.HostConfiguration.GetSection("SQLite");
        }

        var sqliteConfigurationSection = context.PluginConfiguration.GetSection("SQLiteConfiguration");
        if (!sqliteConfigurationSection.Exists())
        {
            sqliteConfigurationSection = context.HostConfiguration.GetSection("SQLiteConfiguration");
        }

        pluginServices.AddSQLiteStorageInfrastructure(config =>
        {
            config.ConnectionString =
                sqliteSection["ConnectionString"]
                ?? sqliteConfigurationSection["ConnectionString"]
                ?? string.Empty;
        });
    }
}
