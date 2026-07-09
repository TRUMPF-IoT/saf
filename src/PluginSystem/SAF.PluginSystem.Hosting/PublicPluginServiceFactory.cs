// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Microsoft.Extensions.DependencyInjection;
using System;

internal sealed class PublicPluginServiceFactory<TService> : IPublicPluginServiceFactory
{
    private readonly object? _serviceKey;
    private readonly ServiceDescriptor _ownedDescriptor;

    private readonly bool _isKeyedService = false;

    private object? _cachedInstance;

    public PublicPluginServiceFactory(ServiceDescriptor ownedDescriptor)
    {
        if(ownedDescriptor.ServiceType != typeof(TService))
        {
            throw new ArgumentException(
                $"The service descriptor's service type '{ownedDescriptor.ServiceType}' does not match the generic type parameter '{typeof(TService)}'.",
                nameof(ownedDescriptor));
        }

        _ownedDescriptor = ownedDescriptor;
    }

    public PublicPluginServiceFactory(ServiceDescriptor ownedDescriptor, object? key)
    {
        if(ownedDescriptor.ServiceType != typeof(TService))
        {
            throw new ArgumentException(
                $"The service descriptor's service type '{ownedDescriptor.ServiceType}' does not match the generic type parameter '{typeof(TService)}'.",
                nameof(ownedDescriptor));
        }

        _ownedDescriptor = ownedDescriptor;
        _serviceKey = key;
        _isKeyedService = true;
    }

    public ServiceDescriptor ServiceDescriptor => _ownedDescriptor;

    public object? Resolve(IServiceProvider serviceProvider)
    {
        if(_isKeyedService)
        {
            return _cachedInstance ??= ResolveKeyedInternal(serviceProvider, _serviceKey);
        }
        else
        {
            return _cachedInstance ??= ResolveInternal(serviceProvider);
        }
    }

    private object? ResolveKeyedInternal(IServiceProvider serviceProvider, object? serviceKey)
    {
        if (_ownedDescriptor.KeyedImplementationInstance is not null)
        {
            return _ownedDescriptor.ImplementationInstance;
        }
        if (_ownedDescriptor.KeyedImplementationFactory is not null)
        {
            return _ownedDescriptor.KeyedImplementationFactory(serviceProvider, serviceKey);
        }
        if (_ownedDescriptor.KeyedImplementationType is not null)
        {
            return ActivatorUtilities.CreateInstance(serviceProvider, _ownedDescriptor.KeyedImplementationType);
        }

        return null;
    }

    private object? ResolveInternal(IServiceProvider serviceProvider)
    {
        if (_ownedDescriptor.ImplementationInstance is not null)
        {
            return _ownedDescriptor.ImplementationInstance;
        }

        if (_ownedDescriptor.ImplementationFactory is not null)
        {
            return _ownedDescriptor.ImplementationFactory(serviceProvider);
        }

        if (_ownedDescriptor.ImplementationType is not null)
        {
            return ActivatorUtilities.CreateInstance(serviceProvider, _ownedDescriptor.ImplementationType);
        }

        return null;
    }
}