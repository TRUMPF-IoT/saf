// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SAF.PluginSystem.Hosting.Contracts;

namespace SAF.Messaging.Nats;

public class PluginManifest : IPluginManifest
{
    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
    {
        var natsConfig = context.PluginConfiguration.GetSection("Nats");
        if (!natsConfig.Exists())
        {
            natsConfig = context.HostConfiguration.GetSection("Nats");
        }

        pluginServices.AddNatsInfrastructure(c => natsConfig.Bind(c));
    }
}
