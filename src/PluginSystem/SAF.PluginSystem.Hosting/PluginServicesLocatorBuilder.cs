// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Microsoft.Extensions.DependencyInjection;
using System;

public record PluginServiceCollection(
    ServiceCollection ServiceCollection,
    List<ServiceDescriptor> PublicServiceDescriptors)
{
    public IServiceProvider? ServiceProvider { get; set; }
}

internal sealed class PluginServicesLocatorBuilder
{
    private readonly PluginServiceCollection _pluginServices;
    private readonly List<PluginServiceCollection> _otherPluginServices;

    public PluginServicesLocatorBuilder(
        PluginServiceCollection pluginServices,
        IEnumerable<PluginServiceCollection> otherPluginServices)
    {
        _pluginServices = pluginServices;
        _otherPluginServices = otherPluginServices.ToList();
    }

    public void Build()
    {
        // patch the plugin's public services to be resolved from the plugin's service provider via the PublicPluginServiceFactory
        PatchPublicPluginServices();

        // add public services of other plugins to this plugin's service collection so that they can be resolved from
        // this plugin's service provider. the ImportedServiceLocator is used to locate the services in its owner plugins.
        ImportServices();

        // Build the plugin's service provider to resolve public services when creating the wrapper instances
        _pluginServices.ServiceProvider = _pluginServices.ServiceCollection.BuildServiceProvider();
    }

    private void PatchPublicPluginServices()
    {
        var serviceCollection = _pluginServices.ServiceCollection;

        _pluginServices.PublicServiceDescriptors.ForEach(sd =>
        {
            if(!serviceCollection.Remove(sd))
            {
                return;
            }

            var factoryType = typeof(PublicPluginServiceFactory<>).MakeGenericType(sd.ServiceType);
            if (sd.IsKeyedService)
            {
                AddKeyedService(serviceCollection, (_, key) => Activator.CreateInstance(factoryType, sd, key)!, sd.Lifetime, factoryType, sd.ServiceKey);
                AddKeyedService(serviceCollection, (sp, _) => CreateInstanceByFactory(sp, sd), sd);
            }
            else
            {
                AddService(serviceCollection, _ => Activator.CreateInstance(factoryType, sd)!, sd.Lifetime, factoryType);
                AddService(serviceCollection, sp => CreateInstanceByFactory(sp, sd), sd);
            }
        });
    }

    private static object CreateInstanceByFactory(IServiceProvider serviceProvider, ServiceDescriptor sd)
    {
        var serviceFactory = serviceProvider.FindPublicPluginServiceFactory(sd) ??
            throw new InvalidOperationException($"Failed to resolve public plugin service factory for service type {sd.ServiceType.FullName} and key {sd.ServiceKey}");

        return serviceFactory.Resolve(serviceProvider)!;
    }

    private void ImportServices()
    {
        foreach (var collection in _otherPluginServices)
        {
            foreach (var publicServiceDescriptor in collection.PublicServiceDescriptors)
            {
                var pluginServiceLocator = new ImportedServiceLocator(collection, publicServiceDescriptor);

                if (publicServiceDescriptor.IsKeyedService)
                {
                    AddKeyedService(_pluginServices.ServiceCollection,
                        (_, key) => ResolveImportedService(pluginServiceLocator, publicServiceDescriptor, key), publicServiceDescriptor);
                }
                else
                {
                    AddService(_pluginServices.ServiceCollection,
                        _ => ResolveImportedService(pluginServiceLocator, publicServiceDescriptor), publicServiceDescriptor);
                }
            }
        }
    }

    private static object ResolveImportedService(ImportedServiceLocator pluginServiceLocator, ServiceDescriptor publicServiceDescriptor, object? serviceKey = null)
    {
        var service = serviceKey is null
            ? pluginServiceLocator.GetService(publicServiceDescriptor.ServiceType)
            : pluginServiceLocator.GetKeyedService(publicServiceDescriptor.ServiceType, serviceKey);

        if (service is null)
        {
            throw new InvalidOperationException($"Failed to resolve imported service for service type {publicServiceDescriptor.ServiceType.FullName} and key {serviceKey}.");
        }

        if (publicServiceDescriptor.Lifetime != ServiceLifetime.Singleton)
        {
            return service;
        }

        return NonOwningServiceProxy.WrapIfRequired(service, publicServiceDescriptor.ServiceType);
    }

    private static void AddKeyedService(IServiceCollection target, Func<IServiceProvider, object?, object> implementationFactory, ServiceDescriptor publicServiceDescriptor)
        => AddKeyedService(target, implementationFactory, publicServiceDescriptor.Lifetime, publicServiceDescriptor.ServiceType, publicServiceDescriptor.ServiceKey);

    private static void AddKeyedService(IServiceCollection target, Func<IServiceProvider, object?, object> implementationFactory, ServiceLifetime serviceLifetime, Type serviceType, object? serviceKey)
    {
        switch (serviceLifetime)
        {
            case ServiceLifetime.Singleton:
                target.AddKeyedSingleton(serviceType, serviceKey, implementationFactory);
                break;
            case ServiceLifetime.Scoped:
                target.AddKeyedScoped(serviceType, serviceKey, implementationFactory);
                break;
            case ServiceLifetime.Transient:
                target.AddKeyedTransient(serviceType, serviceKey, implementationFactory);
                break;
            default:
                throw new InvalidOperationException($"Unsupported service lifetime {serviceLifetime} for service {serviceType.FullName}");
        }
    }

    private static void AddService(IServiceCollection target, Func<IServiceProvider, object> implementationFactory, ServiceDescriptor publicServiceDescriptor)
        => AddService(target, implementationFactory, publicServiceDescriptor.Lifetime, publicServiceDescriptor.ServiceType);

    private static void AddService(IServiceCollection target, Func<IServiceProvider, object> implementationFactory, ServiceLifetime serviceLifetime, Type serviceType)
    {
        switch (serviceLifetime)
        {
            case ServiceLifetime.Singleton:
                target.AddSingleton(serviceType, implementationFactory);
                break;
            case ServiceLifetime.Scoped:
                target.AddScoped(serviceType, implementationFactory);
                break;
            case ServiceLifetime.Transient:
                target.AddTransient(serviceType, implementationFactory);
                break;
            default:
                throw new InvalidOperationException($"Unsupported service lifetime {serviceLifetime} for service {serviceType.FullName}");
        }
    }
}