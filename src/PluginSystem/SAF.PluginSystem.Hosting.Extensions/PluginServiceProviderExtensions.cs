// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace SAF.PluginSystem.Hosting.Extensions;

using Contracts;

public static class PluginServiceProviderExtensions
{
    public static T GetRequiredService<T>(this IPluginServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        return serviceProvider.GetService<T>() ??
               throw new InvalidOperationException($"No service for type '{typeof(T)}' has been registered.");
    }

    public static T GetRequiredKeyedService<T>(this IPluginServiceProvider serviceProvider, string key)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        return serviceProvider.GetKeyedService<T>(key) ??
            throw new InvalidOperationException(
                $"No service for type '{typeof(T)}' with key '{key}' has been registered.");
    }
}