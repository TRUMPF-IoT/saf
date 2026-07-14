// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;

internal static class NonOwningServiceProxy
{
    private static readonly ConcurrentDictionary<Type, Func<object, object>> ProxyFactories = new();

    public static object WrapIfRequired(object service, Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(serviceType);

        if (!serviceType.IsInterface)
        {
            return service;
        }

        if (service is not IDisposable && service is not IAsyncDisposable)
        {
            return service;
        }

        var proxyFactory = ProxyFactories.GetOrAdd(serviceType, CreateProxyFactory);
        return proxyFactory(service);
    }

    private static Func<object, object> CreateProxyFactory(Type serviceType)
    {
        var createProxyMethod = typeof(NonOwningServiceProxy)
            .GetMethod(nameof(CreateProxy), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(serviceType);

        return service => createProxyMethod.Invoke(null, [service])!;
    }

    private static object CreateProxy<TService>(object service)
        where TService : class
    {
        var proxy = DispatchProxy.Create<TService, NonOwningDispatchProxy<TService>>();
        ((NonOwningDispatchProxy<TService>)(object)proxy).Initialize((TService)service);
        return proxy;
    }

    private class NonOwningDispatchProxy<TService> : DispatchProxy
        where TService : class
    {
        private TService? _service;

        public void Initialize(TService service)
        {
            _service = service;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);

            if (_service is null)
            {
                throw new InvalidOperationException("The proxy target service was not initialized.");
            }

            if (targetMethod.Name == nameof(IDisposable.Dispose) && targetMethod.GetParameters().Length == 0)
            {
                return null;
            }

            if (targetMethod.Name == nameof(IAsyncDisposable.DisposeAsync) && targetMethod.GetParameters().Length == 0)
            {
                return ValueTask.CompletedTask;
            }

            try
            {
                return targetMethod.Invoke(_service, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
