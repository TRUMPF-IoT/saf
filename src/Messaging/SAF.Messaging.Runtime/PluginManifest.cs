// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Runtime;

using Microsoft.Extensions.DependencyInjection;
using SAF.Messaging.Contracts;
using SAF.PluginSystem.Hosting.Contracts;

public class PluginManifest : IPluginManifest
{
    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
    {
        var primaryKey = context.PluginConfiguration.GetSection("Messaging")["PrimaryKey"]
                         ?? context.HostConfiguration.GetSection("Messaging")["PrimaryKey"];

        if (string.IsNullOrWhiteSpace(primaryKey))
            throw new InvalidOperationException("Messaging.PrimaryKey must be configured explicitly. Example: Messaging:PrimaryKey = Routing.");

        pluginServices.AddSingleton<IServiceMessageDispatcher, ServiceMessageDispatcher>();
        pluginServices.AddSingleton<IMessagingInfrastructure>(sp => ResolvePrimaryMessagingInfrastructure(sp, primaryKey));
    }

    private static IMessagingInfrastructure ResolvePrimaryMessagingInfrastructure(IServiceProvider serviceProvider, string primaryKey)
    {
        var factory = serviceProvider.GetKeyedService<IMessagingInfrastructureFactory>(primaryKey);
        if (factory == null)
            throw new InvalidOperationException($"No IMessagingInfrastructureFactory registered for Messaging.PrimaryKey '{primaryKey}'.");

        return factory.Create(new MessagingConfiguration { Key = primaryKey });
    }
}


