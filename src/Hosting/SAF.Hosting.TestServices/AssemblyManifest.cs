// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.TestServices
{
    using Microsoft.Extensions.DependencyInjection;
    using Contracts;

    public class AssemblyManifest : IServiceAssemblyManifest
    {
        public string FriendlyName => "Test Assembly";

        public void RegisterDependencies(IServiceCollection services, IServiceHostContext context)
            => services.AddHostedAsync<DummyService>();
    }
}