// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <inheritdoc />
public sealed class PluginServicesContainer(
    ILogger<PluginServicesContainer> logger,
    IPluginSystemHostContext hostContext,
    IServiceProvider applicationServiceProvider,
    IEnumerable<IPluginAssemblyContainer> pluginContainers,
    IPublicServiceTypeRegistry publicServiceTypeRegistry)
    : IPluginServicesContainer, IAsyncDisposable
{
    private readonly Lock _syncPluginLoading = new();
    private bool _initialized = false;
    private bool _disposed = false;

    private readonly List<PluginServiceCollection> _pluginServiceCollections = [];
    private readonly PluginServiceCollection _publicServicesOnlyCollection = new(new ServiceCollection(), []);

    public IEnumerable<IServiceProvider> GetPluginServices()
    {
        InitializeServiceProviders();
        return _pluginServiceCollections.Select(l => l.ServiceProvider!).ToList();
    }

    public IServiceProvider GetPublicServices()
    {
        InitializeServiceProviders();
        return _publicServicesOnlyCollection.ServiceProvider!;
    }

    private void InitializeServiceProviders()
    {
        lock(_syncPluginLoading)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            logger.LogInformation("Starting plug-in search and initialization.");

            var manifests = pluginContainers.SelectMany(s => s.GetPluginManifests()).ToList();
            InitializePlugins(manifests);
        }
    }

    private void InitializePlugins(IEnumerable<IPluginManifest> pluginManifests)
    {
        foreach (var manifest in pluginManifests)
        {
            var pluginServices = new ServiceCollection();

            applicationServiceProvider.RedirectCommonServices(pluginServices);

            manifest.ConfigureServices(hostContext, pluginServices);

            _pluginServiceCollections.Add(new PluginServiceCollection(pluginServices, CollectPublicServices(pluginServices)));
        }

        var publicServicesOnlyBuilder = new PluginServicesLocatorBuilder(_publicServicesOnlyCollection, _pluginServiceCollections);

        foreach (var pluginServices in _pluginServiceCollections)
        {
            var otherPluginServiceCollections = _pluginServiceCollections.Except([pluginServices]);
            var builder = new PluginServicesLocatorBuilder(pluginServices, otherPluginServiceCollections);
            builder.Build();
        }

        publicServicesOnlyBuilder.Build();
    }

    private List<ServiceDescriptor> CollectPublicServices(IEnumerable<ServiceDescriptor> serviceCollection)
    {
        var publicServiceDescriptors = serviceCollection
            .Where(sd => publicServiceTypeRegistry.GetAssemblyNames().FirstOrDefault(a => a == sd.ServiceType.Assembly.FullName) != null);
        return publicServiceDescriptors.ToList();
    }

    public async ValueTask DisposeAsync()
    {
        List<IServiceProvider> providersToDispose;

        lock (_syncPluginLoading)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            providersToDispose = _pluginServiceCollections
                .Select(collection => collection.ServiceProvider)
                .Append(_publicServicesOnlyCollection.ServiceProvider)
                .OfType<IServiceProvider>()
                .ToList();
        }

        foreach (IServiceProvider provider in providersToDispose)
        {
            await DisposeProviderAsync(provider).ConfigureAwait(false);
        }
    }

    private static async ValueTask DisposeProviderAsync(IServiceProvider provider)
    {
        if (provider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            return;
        }

        if (provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}