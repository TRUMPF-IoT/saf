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

    private List<PluginServiceCollection> _pluginServiceCollections = [];
    private PluginServiceCollection _publicServicesOnlyCollection = new(new ServiceCollection(), []);

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

    public async ValueTask ReinitializeAsync(CancellationToken cancellationToken = default)
    {
        List<IServiceProvider> providersToDispose;

        lock (_syncPluginLoading)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            cancellationToken.ThrowIfCancellationRequested();

            logger.LogInformation("Reinitializing plug-in service providers.");

            var (pluginServiceCollections, publicServicesOnlyCollection) = BuildProviders();
            providersToDispose = SnapshotProviders();
            _pluginServiceCollections = pluginServiceCollections;
            _publicServicesOnlyCollection = publicServicesOnlyCollection;
            _initialized = true;
        }

        foreach (IServiceProvider provider in providersToDispose)
        {
            await DisposeProviderAsync(provider).ConfigureAwait(false);
        }
    }

    private void InitializeServiceProviders()
    {
        lock(_syncPluginLoading)
        {
            if (_initialized)
            {
                return;
            }

            logger.LogInformation("Starting plug-in search and initialization.");

            var (pluginServiceCollections, publicServicesOnlyCollection) = BuildProviders();
            _pluginServiceCollections = pluginServiceCollections;
            _publicServicesOnlyCollection = publicServicesOnlyCollection;
            _initialized = true;
        }
    }

    private (List<PluginServiceCollection> PluginServiceCollections, PluginServiceCollection PublicServicesOnlyCollection) BuildProviders()
    {
        var manifests = pluginContainers.SelectMany(s => s.GetPluginManifests()).ToList();
        return InitializePlugins(manifests);
    }

    private (List<PluginServiceCollection> PluginServiceCollections, PluginServiceCollection PublicServicesOnlyCollection) InitializePlugins(IEnumerable<IPluginManifest> pluginManifests)
    {
        List<PluginServiceCollection> pluginServiceCollections = [];
        PluginServiceCollection publicServicesOnlyCollection = new(new ServiceCollection(), []);

        try
        {
            foreach (var manifest in pluginManifests)
            {
                var pluginServices = new ServiceCollection();

                applicationServiceProvider.RedirectCommonServices(pluginServices);

                manifest.ConfigureServices(hostContext, pluginServices);

                pluginServiceCollections.Add(new PluginServiceCollection(pluginServices, CollectPublicServices(pluginServices)));
            }

            var publicServicesOnlyBuilder = new PluginServicesLocatorBuilder(publicServicesOnlyCollection, pluginServiceCollections);

            foreach (var pluginServices in pluginServiceCollections)
            {
                var otherPluginServiceCollections = pluginServiceCollections.Except([pluginServices]);
                var builder = new PluginServicesLocatorBuilder(pluginServices, otherPluginServiceCollections);
                builder.Build();
            }

            publicServicesOnlyBuilder.Build();
            return (pluginServiceCollections, publicServicesOnlyCollection);
        }
        catch
        {
            DisposeProviders(SnapshotProviders(pluginServiceCollections, publicServicesOnlyCollection));
            throw;
        }
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

            providersToDispose = SnapshotProviders();
        }

        foreach (IServiceProvider provider in providersToDispose)
        {
            await DisposeProviderAsync(provider).ConfigureAwait(false);
        }
    }

    private List<IServiceProvider> SnapshotProviders() =>
        SnapshotProviders(_pluginServiceCollections, _publicServicesOnlyCollection);

    private static List<IServiceProvider> SnapshotProviders(
        IEnumerable<PluginServiceCollection> pluginServiceCollections,
        PluginServiceCollection publicServicesOnlyCollection) =>
        [.. pluginServiceCollections
            .Select(collection => collection.ServiceProvider)
            .Append(publicServicesOnlyCollection.ServiceProvider)
            .OfType<IServiceProvider>()];

    private static void DisposeProviders(IEnumerable<IServiceProvider> providers)
    {
        foreach (IServiceProvider provider in providers)
        {
            DisposeProvider(provider);
        }
    }

    private static void DisposeProvider(IServiceProvider provider)
    {
        if (provider is IAsyncDisposable asyncDisposable)
        {
            asyncDisposable.DisposeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            return;
        }

        if (provider is IDisposable disposable)
        {
            disposable.Dispose();
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