// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace SAF.PluginSystem.Hosting;

using Microsoft.Extensions.DependencyInjection;
using System;

internal class ImportedServiceLocator
{
    private readonly PluginServiceCollection _pluginServiceCollection;
    private readonly ServiceDescriptor _serviceDescriptor;

    public ImportedServiceLocator(PluginServiceCollection pluginServiceCollection, ServiceDescriptor serviceDescriptor)
    {
        _pluginServiceCollection = pluginServiceCollection;
        _serviceDescriptor = serviceDescriptor;
    }

    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        var service = ResolveLocalPluginService(serviceType);
        if (service is not null)
        {
            return service;
        }

        return null;
    }

    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        var service = ResolveLocalPluginService(serviceType, serviceKey);
        if (service is not null)
        {
            return service;
        }

        return null;
    }

    private object? ResolveLocalPluginService(Type serviceType, object? serviceKey)
    {
        if (serviceKey is not null && _serviceDescriptor.ServiceKey is not null && !serviceKey.Equals(_serviceDescriptor.ServiceKey))
        {
            throw new ArgumentException($"The requested service key {serviceType.FullName} - {serviceKey}" +
                $"does not match the service descriptor's service key {_serviceDescriptor.ServiceType.FullName} - {_serviceDescriptor.ServiceKey}.",
                nameof(serviceKey));
        }

        return ResolveLocalPluginService(serviceType);
    }

    private object? ResolveLocalPluginService(Type serviceType)
    {
        if (serviceType != _serviceDescriptor.ServiceType)
        {
            throw new ArgumentException($"The requested service type {serviceType.FullName}" +
                $"does not match the service descriptor's service type {_serviceDescriptor.ServiceType.FullName}.", nameof(serviceType));
        }

        var localLocator = _pluginServiceCollection.ServiceProvider?.FindPublicPluginServiceFactory(_serviceDescriptor);
        return localLocator?.Resolve(_pluginServiceCollection.ServiceProvider!);
    }
}