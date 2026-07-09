// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace SAF.PluginSystem.Hosting;

using Contracts;
using Microsoft.Extensions.DependencyInjection;

/// <inheritdoc />
public class PluginServiceProvider(IPluginServicesContainer pluginLoader) : IPluginServiceProvider
{
    public T? GetService<T>() => GetServices<T>().SingleOrDefault();
    public T? GetKeyedService<T>(string key) => GetKeyedServices<T>(key).SingleOrDefault();

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