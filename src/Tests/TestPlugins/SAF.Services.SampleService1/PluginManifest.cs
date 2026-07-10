// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Services.SampleService1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AnyOtherInternalLogic;
using MessageHandlers;
using SAF.Messaging.Contracts;
using SAF.Messaging.Extensions;
using SAF.PluginSystem.Hosting.Contracts;
using SAF.PluginSystem.Hosting.Extensions;
using Toolbox;

public class PluginManifest : IPluginManifest
{
    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
    {
        // dependencies
        pluginServices.AddTransient<MyInternalDependency>();
        pluginServices.AddSingleton<IMessageHandler, CatchAllMessageHandler>();
        pluginServices.AddSingleton<IMessageHandler, PingMessageHandler>();
        pluginServices.AddMessageHandlerResolver();

        // "microservice" settings
        var serviceConfigRoot = context.PluginConfiguration.GetSection(nameof(MySpecialService)).Exists()
            ? context.PluginConfiguration
            : context.HostConfiguration;

        pluginServices.AddServiceConfiguration<MyServiceConfiguration>(serviceConfigRoot, nameof(MySpecialService));

        // "microservices"
        pluginServices.AddServicePlugin<MySpecialService>();
    }
}