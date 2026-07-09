// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.InProcess;
using Microsoft.Extensions.DependencyInjection;
using Common;

public class AssemblyManifest : IMessagingAssemblyManifest
{
    public void RegisterDependencies(IServiceCollection services, MessagingConfiguration config)
    {
        services.AddInProcessMessagingInfrastructure(config);
    }
}