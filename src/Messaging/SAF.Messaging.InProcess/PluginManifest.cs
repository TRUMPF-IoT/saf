// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.InProcess;
using Microsoft.Extensions.DependencyInjection;
using SAF.Common;
using SAF.Messaging.Contracts;
using SAF.PluginSystem.Hosting.Contracts;

public class PluginManifest : IPluginManifest
{
    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
    {
        pluginServices.AddInProcessMessagingInfrastructure();
    }
}


