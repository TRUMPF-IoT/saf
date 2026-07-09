// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace TestPlugin.PluginB;

using Microsoft.Extensions.DependencyInjection;
using TestPlugin.PublicDependencyA;

public class PublicSingletonB(IServiceProvider serviceProvider) : IPublicSingleton
{
    public object? GetPrivateSingleton() => serviceProvider.GetRequiredService<PrivateSingletonB>();
    public Type GetPrivateSingletonType() => typeof(PrivateSingletonB);
    public object? GetPrivateTransient() => serviceProvider.GetRequiredService<PrivateTransientB>();
    public Type GetPrivateTransientType() => typeof(PrivateTransientB);

    public object? GetPrivateServiceOfOtherPlugin() => serviceProvider.GetKeyedService<TimeProvider>("tpA");
}
