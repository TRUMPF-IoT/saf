// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;
using Microsoft.Extensions.DependencyInjection;

/// <inheritdoc />
public class PluginServiceProvider(IPluginServicesContainer pluginLoader) : IPluginServiceProvider
{
    // Deciding on the candidate count instead of the resolved value: for an unconstrained T, `value ??
    // throw` boxes value types for the null test, and a boxed struct is never null, so GetRequiredService<T>
    // for a value type T would never throw and silently return default(T) instead - indistinguishable from
    // a legitimately registered zero value.
    public T? GetService<T>()
    {
        var candidates = GetServices<T>().Take(2).ToList();
        return candidates.Count switch
        {
            0 => default,
            1 => candidates[0],
            _ => throw new InvalidOperationException(
                $"More than one service for type '{typeof(T)}' is registered across the plugin containers.")
        };
    }

    public T? GetKeyedService<T>(string key)
    {
        var candidates = GetKeyedServices<T>(key).Take(2).ToList();
        return candidates.Count switch
        {
            0 => default,
            1 => candidates[0],
            _ => throw new InvalidOperationException(
                $"More than one service for type '{typeof(T)}' with key '{key}' is registered across the plugin containers.")
        };
    }

    public T GetRequiredService<T>()
    {
        var candidates = GetServices<T>().Take(2).ToList();
        return candidates.Count switch
        {
            1 => candidates[0],
            0 => throw new InvalidOperationException($"No service for type '{typeof(T)}' has been registered."),
            _ => throw new InvalidOperationException(
                $"More than one service for type '{typeof(T)}' is registered across the plugin containers.")
        };
    }

    public T GetRequiredKeyedService<T>(string key)
    {
        var candidates = GetKeyedServices<T>(key).Take(2).ToList();
        return candidates.Count switch
        {
            1 => candidates[0],
            0 => throw new InvalidOperationException(
                $"No service for type '{typeof(T)}' with key '{key}' has been registered."),
            _ => throw new InvalidOperationException(
                $"More than one service for type '{typeof(T)}' with key '{key}' is registered across the plugin containers.")
        };
    }

    public IEnumerable<T> GetServices<T>()
    {
        var pluginServiceProviders = pluginLoader.GetPublicServices();
        return pluginServiceProviders.GetServices<T>(); 
    }
    public IEnumerable<T> GetKeyedServices<T>(string key)
    {
        var pluginServiceProviders = pluginLoader.GetPublicServices();
        return pluginServiceProviders.GetKeyedServices<T>(key);
    }
}