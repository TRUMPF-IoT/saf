// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.DependencyInjection;
using SAF.Common;

namespace SAF.Messaging.Cde
{
    public class AssemblyManifest : IMessagingAssemblyManifest
    {
        public void RegisterDependencies(IServiceCollection services, MessagingConfiguration config)
        {
            services.AddCdeMessagingInfrastructure(config);
        }
    }
}
