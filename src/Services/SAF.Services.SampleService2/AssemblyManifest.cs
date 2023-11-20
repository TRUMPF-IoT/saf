// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.DependencyInjection;
using SAF.Hosting.Abstractions;

namespace SAF.Services.SampleService2;

public class AssemblyManifest : IServiceAssemblyManifest
{
    public string FriendlyName => "Meine zweite Assembly";

    public void RegisterDependencies(IServiceCollection services, IServiceHostContext context)
    {
        services.AddHostedAsync<MyService>();
    }
}