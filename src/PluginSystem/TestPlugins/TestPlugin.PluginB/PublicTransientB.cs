// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace TestPlugin.PluginB;

using Microsoft.Extensions.DependencyInjection;
using System;
using TestPlugin.PublicDependencyA;

public class PublicTransientB(IServiceProvider? serviceProvider) : IPublicTransient
{
    public object? GetPrivateSingleton() => serviceProvider?.GetRequiredService<PrivateSingletonB>();
    public object? GetPrivateTransient() => serviceProvider?.GetRequiredService<PrivateTransientB>();

    public object? GetPrivateServiceOfOtherPlugin() => serviceProvider?.GetKeyedService<TimeProvider>("tpA");
}
