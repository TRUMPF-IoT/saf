// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Threading;

public class PublicPluginServiceFactoryTests
{
    private interface ITestService;

    private sealed class TestService : ITestService;

    [Fact]
    public async Task Resolve_ShouldCreateSingletonOnlyOnce_WhenCalledConcurrently()
    {
        // Arrange
        var invocationCount = 0;
        ServiceDescriptor serviceDescriptor = ServiceDescriptor.Singleton(typeof(ITestService), _ =>
        {
            Interlocked.Increment(ref invocationCount);
            return new TestService();
        });

        object publicPluginServiceFactory = CreateFactoryInstance(typeof(ITestService), serviceDescriptor);

        using ServiceProvider serviceProvider = new ServiceCollection().BuildServiceProvider();
        using ManualResetEventSlim startGate = new(false);

        IEnumerable<Task<object?>> resolveTasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() =>
            {
                startGate.Wait();
                return Resolve(publicPluginServiceFactory, serviceProvider);
            }));

        // Act
        startGate.Set();
        await Task.WhenAll(resolveTasks);

        // Assert
        Assert.Equal(1, Volatile.Read(ref invocationCount));
    }

    private static object CreateFactoryInstance(Type serviceType, ServiceDescriptor serviceDescriptor)
    {
        Assembly hostingAssembly = typeof(PluginServicesContainer).Assembly;
        Type publicFactoryTypeDefinition = hostingAssembly.GetType("SAF.PluginSystem.Hosting.PublicPluginServiceFactory`1", throwOnError: true)!;
        Type closedFactoryType = publicFactoryTypeDefinition.MakeGenericType(serviceType);

        return Activator.CreateInstance(closedFactoryType, serviceDescriptor)
            ?? throw new InvalidOperationException($"Failed to create factory instance for service type {serviceType.FullName}.");
    }

    private static object? Resolve(object publicPluginServiceFactory, IServiceProvider serviceProvider)
    {
        MethodInfo resolveMethod = publicPluginServiceFactory.GetType().GetMethod(nameof(IPublicPluginServiceFactory.Resolve))
            ?? throw new InvalidOperationException("Failed to find Resolve method on public plugin service factory.");

        return resolveMethod.Invoke(publicPluginServiceFactory, [serviceProvider]);
    }
}
