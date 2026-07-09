// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Common;
using SAF.PluginSystem.Hosting.Contracts;

public class PluginManifest : IMessagingAssemblyManifest, IPluginManifest
{
    public void RegisterDependencies(IServiceCollection services, MessagingConfiguration config)
    {
        services.AddRedisMessagingInfrastructure(config);
    }

    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
    {
        pluginServices.AddRedisInfrastructure(c => context.HostConfiguration.GetSection("Redis").Bind(c));
    }
}