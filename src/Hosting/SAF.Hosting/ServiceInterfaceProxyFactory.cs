// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;

using System;
using System.Reflection;

internal static class ServiceInterfaceProxyFactory
{
    /// <summary>
    /// Creates a proxy for the specified target object if it implements <see cref="IDisposable"/> or <see
    /// cref="IAsyncDisposable"/>; otherwise, returns the target object directly.
    /// </summary>
    /// <remarks>If the target object does not implement <see cref="IDisposable"/> or <see
    /// cref="IAsyncDisposable"/>, no proxy is created and the original object is returned. This method is typically
    /// used to enable interception of disposal operations on service interfaces.</remarks>
    /// <typeparam name="T">The interface type of the target object. Must be a reference type and an interface.</typeparam>
    /// <param name="target">The target object to proxy. If the object implements <see cref="IDisposable"/> or <see
    /// cref="IAsyncDisposable"/>, a proxy is created; otherwise, the object is returned as-is. Cannot be <see
    /// langword="null"/>.</param>
    /// <returns>A proxy instance of type <typeparamref name="T"/> if the target implements <see cref="IDisposable"/> or <see
    /// cref="IAsyncDisposable"/>; otherwise, the original target object.</returns>
    /// <exception cref="ArgumentException">Thrown if <typeparamref name="T"/> is not an interface type.</exception>
    public static T Create<T>(T target) where T : class
    {
        if(target is not IDisposable && target is not IAsyncDisposable)
        {
            // No proxy needed if target does not implement IDisposable
            return target;
        }

        ArgumentNullException.ThrowIfNull(target);
        if (!typeof(T).IsInterface)
            throw new ArgumentException($"Type '{typeof(T)}' must be an interface.", nameof(target));

        var proxy = DispatchProxy.Create<T, ServiceInterfaceProxy<T>>();
        var instance = proxy as ServiceInterfaceProxy<T>;
        instance?.SetTarget(target);

        return proxy;
    }

    /// <summary>
    /// Generic delegating proxy that implements an interface <typeparamref name="T"/> and forwards all calls
    /// to a provided target instance that actually implements the interface.
    /// </summary>
    /// <typeparam name="T">The interface type to implement. Must be an interface.</typeparam>
#pragma warning disable S3260 // Non-derived "private" classes and records should be "sealed"
    // Must not be sealed to work with DispatchProxy.
    private class ServiceInterfaceProxy<T> : DispatchProxy where T : class
#pragma warning restore S3260 // Non-derived "private" classes and records should be "sealed"
    {
        private T? _target;

        public void SetTarget(T target)
        {
            ArgumentNullException.ThrowIfNull(target);
            _target = target;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (_target is null)
            {
                throw new InvalidOperationException("Proxy target not initialized.");
            }

            try
            {
                return targetMethod.Invoke(_target, args);
            }
            catch (TargetInvocationException tie)
            {
                // Unwrap the inner exception thrown by reflection
                throw tie.InnerException ?? tie;
            }
        }
    }
}