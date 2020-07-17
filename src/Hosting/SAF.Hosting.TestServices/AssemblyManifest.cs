// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.DependencyInjection;
using SAF.Common;

namespace SAF.Hosting.TestServices
{
    public class AssemblyManifest : IServiceAssemblyManifest
    {
        public string FriendlyName => "Test Assembly";

        public void RegisterDependencies(IServiceCollection services)
        {
            services.AddHosted<DummyService>();
        }

        public void RegisterDependencies(IServiceCollection services, IServiceHostContext context)
            => RegisterDependencies(services);
    }
}