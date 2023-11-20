// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.DependencyInjection;
using SAF.Hosting.Abstractions;
using SAF.Services.SampleService1.AnyOtherInternalLogic;
using SAF.Services.SampleService1.MessageHandlers;
using SAF.Toolbox;

namespace SAF.Services.SampleService1;

public class AssemblyManifest : IServiceAssemblyManifest
{
    public string FriendlyName => "My special services.";

    public void RegisterDependencies(IServiceCollection services, IServiceHostContext context)
    {
        // dependencies
        services.AddTransient<MyInternalDependency>();
        services.AddTransient<CatchAllMessageHandler>();
        services.AddTransient<PingMessageHandler>();

        // "microservice" settings
        services.AddServiceConfiguration<MyServiceConfiguration>(context.Configuration, nameof(MySpecialService));

        // "microservices"
        services.AddHosted<MySpecialService>();
    }
}