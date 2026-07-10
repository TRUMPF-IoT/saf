// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SAF.PluginSystem.Hosting.Contracts;

public class PluginManifest : IPluginManifest
{
    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
    {
        var redisConfig = context.PluginConfiguration.GetSection("Redis");
        if (!redisConfig.Exists())
        {
            redisConfig = context.HostConfiguration.GetSection("Redis");
        }

        pluginServices.AddRedisInfrastructure(c => redisConfig.Bind(c));
    }
}