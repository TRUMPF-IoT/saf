// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Services.SampleService1;
using Microsoft.Extensions.DependencyInjection;
using AnyOtherInternalLogic;
using MessageHandlers;
using SAF.Hosting.Contracts;
using SAF.PluginSystem.Hosting.Contracts;
using Toolbox;

public class PluginManifest : IPluginManifest
{
    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
    {
        // dependencies
        pluginServices.AddTransient<MyInternalDependency>();
        pluginServices.AddTransient<CatchAllMessageHandler>();
        pluginServices.AddTransient<PingMessageHandler>();

        // "microservice" settings
        pluginServices.AddServiceConfiguration<MyServiceConfiguration>(context.HostConfiguration, nameof(MySpecialService));

        // "microservices"
        pluginServices.AddHostedAsync<MySpecialService>();
    }
}