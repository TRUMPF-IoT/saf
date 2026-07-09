// SPDX-FileCopyrightText: 2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace TestPlugin.PluginB;

using Microsoft.Extensions.DependencyInjection;
using TestPlugin.PublicDependencyA;
using SAF.PluginSystem.Hosting.Contracts;

public class PluginManifest : IPluginManifest
{
    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(pluginServices);

        pluginServices.AddSingleton<PrivateSingletonB>();
        pluginServices.AddTransient<PrivateTransientB>();

        pluginServices.AddSingleton<IPublicSingleton>(sp => new PublicSingletonB(sp));
        pluginServices.AddKeyedSingleton<IPublicSingleton>("ssB", (sp, _) => new PublicSingletonB(sp));
        pluginServices.AddTransient<IPublicTransient, PublicTransientB>();
        pluginServices.AddKeyedTransient<IPublicTransient, PublicTransientB>("ssB");

        pluginServices.AddKeyedSingleton("tpB", TimeProvider.System);
    }
}