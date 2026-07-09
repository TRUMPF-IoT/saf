// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SAF.Common;
using SAF.PluginSystem.Hosting.Contracts;

namespace SAF.Messaging.Nats;

public class PluginManifest : IMessagingAssemblyManifest, IPluginManifest
{
    public void RegisterDependencies(IServiceCollection services, MessagingConfiguration config)
    {
        services.AddNatsMessagingInfrastructure(config);
    }

    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
    {
        pluginServices.AddNatsMessagingInfrastructure(c => context.HostConfiguration.GetSection("Nats").Bind(c));
        pluginServices.AddSingleton<IMessagingInfrastructure>(sp => sp.GetRequiredService<INatsMessagingInfrastructure>());
    }
}
