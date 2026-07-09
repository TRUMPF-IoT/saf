// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace TestPlugin.PluginA;

using Microsoft.Extensions.DependencyInjection;
using TestPlugin.PublicDependencyA;

public class PublicSingletonA(IServiceProvider serviceProvider) : IPublicSingleton
{
    public object? GetPrivateSingleton() => serviceProvider.GetRequiredService<PrivateSingletonA>();
    public Type GetPrivateSingletonType() => typeof(PrivateSingletonA);
    public object? GetPrivateTransient() => serviceProvider.GetRequiredService<PrivateTransientA>();
    public Type GetPrivateTransientType() => typeof(PrivateTransientA);

    public object? GetPrivateServiceOfOtherPlugin() => serviceProvider.GetKeyedService<TimeProvider>("tpB");
}
