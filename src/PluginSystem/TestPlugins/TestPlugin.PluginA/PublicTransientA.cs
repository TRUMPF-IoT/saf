// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace TestPlugin.PluginA;

using Microsoft.Extensions.DependencyInjection;
using System;
using TestPlugin.PublicDependencyA;

public class PublicTransientA(IServiceProvider serviceProvider) : IPublicTransient
{
    public object? GetPrivateSingleton() => serviceProvider.GetRequiredService<PrivateSingletonA>();
    public object? GetPrivateTransient() => serviceProvider.GetRequiredService<PrivateTransientA>();

    public object? GetPrivateServiceOfOtherPlugin() => serviceProvider.GetKeyedService<TimeProvider>("tpB");
}
