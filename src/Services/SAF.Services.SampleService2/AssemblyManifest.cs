// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Services.SampleService2;
using Microsoft.Extensions.DependencyInjection;
using Hosting.Contracts;

public class AssemblyManifest : IServiceAssemblyManifest
{
    public string FriendlyName => "Meine zweite Assembly";

    public void RegisterDependencies(IServiceCollection services, IServiceHostContext context)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        services.AddHosted<MyService>();
#pragma warning restore CS0618 // Type or member is obsolete
    }
}