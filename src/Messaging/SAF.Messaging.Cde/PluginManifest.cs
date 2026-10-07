// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Cde;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SAF.Cde;
using SAF.Messaging.Cde.Diagnostics;
using SAF.PluginSystem.Hosting.Contracts;

public class PluginManifest : IPluginManifest
{
    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
    {
        var cdeConfig = context.GetCdeConfigurationSection();

        pluginServices.AddCdeInfrastructure(c => cdeConfig.Bind(c));

        if (cdeConfig.GetValue<bool>("EnableDiagnostics"))
        {
            pluginServices.AddCdeDiagnostics();
        }
    }
}