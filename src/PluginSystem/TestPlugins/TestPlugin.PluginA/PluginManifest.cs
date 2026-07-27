// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace TestPlugin.PluginA;

using Microsoft.Extensions.DependencyInjection;
using TestPlugin.PublicDependencyA;
using SAF.PluginSystem.Hosting.Contracts;

public class PluginManifest : IPluginManifest
{
    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(pluginServices);

        pluginServices.AddSingleton<PrivateSingletonA>();
        pluginServices.AddTransient<PrivateTransientA>();

        pluginServices.AddSingleton<IPublicSingleton, PublicSingletonA>();
        pluginServices.AddKeyedSingleton<IPublicSingleton, PublicSingletonA>("ssA");
        pluginServices.AddTransient<IPublicTransient, PublicTransientA>();
        pluginServices.AddKeyedTransient<IPublicTransient, PublicTransientA>("ssA");

        pluginServices.AddKeyedSingleton("tpA", TimeProvider.System);
    }
}