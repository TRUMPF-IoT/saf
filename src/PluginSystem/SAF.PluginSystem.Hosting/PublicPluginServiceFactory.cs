// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Microsoft.Extensions.DependencyInjection;
using System;

internal sealed class PublicPluginServiceFactory<TService> : IPublicPluginServiceFactory, IDisposable, IAsyncDisposable
{
    private readonly object? _serviceKey;
    private readonly ServiceDescriptor _ownedDescriptor;

    private readonly bool _isKeyedService = false;
    private readonly Lock _cacheLock = new();

    private object? _cachedInstance;
    private bool _ownsCachedInstance;
    private bool _disposed;

    public PublicPluginServiceFactory(ServiceDescriptor ownedDescriptor)
    {
        if (ownedDescriptor.ServiceType != typeof(TService))
        {
            throw new ArgumentException(
                $"The service descriptor's service type '{ownedDescriptor.ServiceType}' does not match the generic type parameter '{typeof(TService)}'.",
                nameof(ownedDescriptor));
        }

        _ownedDescriptor = ownedDescriptor;
    }

    public PublicPluginServiceFactory(ServiceDescriptor ownedDescriptor, object? key)
    {
        if (ownedDescriptor.ServiceType != typeof(TService))
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
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_cachedInstance is not null)
        {
            return _cachedInstance;
        }

        lock (_cacheLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_cachedInstance is not null)
            {
                return _cachedInstance;
            }

            var (instance, ownsInstance) = _isKeyedService
                ? ResolveKeyedInternal(serviceProvider, _serviceKey)
                : ResolveInternal(serviceProvider);

            _cachedInstance = instance;
            _ownsCachedInstance = ownsInstance && instance is not null;
            return _cachedInstance;
        }
    }

    public void Dispose()
    {
        var (cachedInstance, ownsCachedInstance) = DetachCachedInstance();

        if (!ownsCachedInstance || cachedInstance is null)
        {
            return;
        }

        if (cachedInstance is IAsyncDisposable asyncDisposable)
        {
            asyncDisposable.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
            return;
        }

        if (cachedInstance is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        var (cachedInstance, ownsCachedInstance) = DetachCachedInstance();

        if (!ownsCachedInstance || cachedInstance is null)
        {
            return;
        }

        if (cachedInstance is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            return;
        }

        if (cachedInstance is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private (object? CachedInstance, bool OwnsCachedInstance) DetachCachedInstance()
    {
        lock (_cacheLock)
        {
            if (_disposed)
            {
                return (null, false);
            }

            _disposed = true;
            var cachedInstance = _cachedInstance;
            var ownsCachedInstance = _ownsCachedInstance;
            _cachedInstance = null;
            _ownsCachedInstance = false;
            return (cachedInstance, ownsCachedInstance);
        }
    }

    private (object? Instance, bool OwnsInstance) ResolveKeyedInternal(IServiceProvider serviceProvider, object? serviceKey)
    {
        if (_ownedDescriptor.KeyedImplementationInstance is not null)
        {
            return (_ownedDescriptor.KeyedImplementationInstance, false);
        }

        if (_ownedDescriptor.KeyedImplementationFactory is not null)
        {
            return (_ownedDescriptor.KeyedImplementationFactory(serviceProvider, serviceKey), true);
        }

        if (_ownedDescriptor.KeyedImplementationType is not null)
        {
            return (ActivatorUtilities.CreateInstance(serviceProvider, _ownedDescriptor.KeyedImplementationType), true);
        }

        return (null, false);
    }

    private (object? Instance, bool OwnsInstance) ResolveInternal(IServiceProvider serviceProvider)
    {
        if (_ownedDescriptor.ImplementationInstance is not null)
        {
            return (_ownedDescriptor.ImplementationInstance, false);
        }

        if (_ownedDescriptor.ImplementationFactory is not null)
        {
            return (_ownedDescriptor.ImplementationFactory(serviceProvider), true);
        }

        if (_ownedDescriptor.ImplementationType is not null)
        {
            return (ActivatorUtilities.CreateInstance(serviceProvider, _ownedDescriptor.ImplementationType), true);
        }

        return (null, false);
    }
}