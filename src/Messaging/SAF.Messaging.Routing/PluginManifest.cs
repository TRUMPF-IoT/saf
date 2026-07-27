// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Routing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SAF.PluginSystem.Hosting.Contracts;

public class PluginManifest : IPluginManifest
{
    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
    {
        var routingConfig = context.PluginConfiguration.GetSection("MessageRouting");
        if (!routingConfig.Exists())
        {
            routingConfig = context.HostConfiguration.GetSection("MessageRouting");
        }

        pluginServices.AddRoutingMessagingInfrastructure(config =>
            routingConfig.Bind(config));
    }
}