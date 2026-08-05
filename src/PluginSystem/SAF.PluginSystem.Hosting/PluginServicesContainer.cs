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
    private bool _initialized;
    private bool _disposed;
    private List<IPluginManifest>? _cachedPluginManifests;

    /// <inheritdoc />
    public bool IsInitialized
    {
        get { lock (_syncPluginLoading) { return _initialized; } }
    }

    private List<PluginServiceCollection> _pluginServiceCollections = [];
    private PluginServiceCollection _publicServicesOnlyCollection = new(new ServiceCollection(), []);

    public IEnumerable<IServiceProvider> GetPluginServices()
    {
        var (pluginServices, _) = GetServiceProviders();
        return pluginServices;
    }

    public IServiceProvider GetPublicServices()
    {
        var (_, publicServices) = GetServiceProviders();
        return publicServices;
    }

    public async ValueTask ReinitializeAsync(CancellationToken cancellationToken = default)
    {
        List<IPluginManifest> pluginManifests;

        lock (_syncPluginLoading)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            cancellationToken.ThrowIfCancellationRequested();

            logger.LogInformation("Reinitializing plug-in service providers.");
            pluginManifests = GetPluginManifests();
        }

        var (pluginServiceCollections, publicServicesOnlyCollection) = InitializePlugins(pluginManifests);
        List<IServiceProvider> providersToDispose;
        Exception? deferredException = null;

        lock (_syncPluginLoading)
        {
            if (_disposed)
            {
                providersToDispose = SnapshotProviders(pluginServiceCollections, publicServicesOnlyCollection);
                deferredException = new ObjectDisposedException(nameof(PluginServicesContainer));
            }
            else if (cancellationToken.IsCancellationRequested)
            {
                providersToDispose = SnapshotProviders(pluginServiceCollections, publicServicesOnlyCollection);
                deferredException = new OperationCanceledException(cancellationToken);
            }
            else
            {
                providersToDispose = SnapshotProviders();
                _pluginServiceCollections = pluginServiceCollections;
                _publicServicesOnlyCollection = publicServicesOnlyCollection;
                _initialized = true;
            }
        }

        await DisposeProvidersAsync(providersToDispose).ConfigureAwait(false);

        if (deferredException is not null)
        {
            throw deferredException;
        }
    }

    private (List<IServiceProvider> PluginServices, IServiceProvider PublicServices) GetServiceProviders()
    {
        List<IPluginManifest> pluginManifests;

        lock (_syncPluginLoading)
        {
            if (_initialized)
            {
                return SnapshotCurrentServiceProviders();
            }

            ObjectDisposedException.ThrowIf(_disposed, this);

            logger.LogInformation("Starting plug-in search and initialization.");
            pluginManifests = GetPluginManifests();
        }

        var (pluginServiceCollections, publicServicesOnlyCollection) = InitializePlugins(pluginManifests);
        List<IServiceProvider> providersToDispose = [];
        bool isDisposed;
        (List<IServiceProvider> PluginServices, IServiceProvider PublicServices) currentProviders;

        lock (_syncPluginLoading)
        {
            isDisposed = _disposed;

            if (isDisposed)
            {
                providersToDispose = SnapshotProviders(pluginServiceCollections, publicServicesOnlyCollection);
            }
            else if (!_initialized)
            {
                _pluginServiceCollections = pluginServiceCollections;
                _publicServicesOnlyCollection = publicServicesOnlyCollection;
                _initialized = true;
            }
            else
            {
                providersToDispose = SnapshotProviders(pluginServiceCollections, publicServicesOnlyCollection);
            }

            currentProviders = SnapshotCurrentServiceProviders();
        }

        DisposeProviders(providersToDispose);

        if (isDisposed)
        {
            throw new ObjectDisposedException(nameof(PluginServicesContainer));
        }

        return currentProviders;
    }

    private List<IPluginManifest> GetPluginManifests() =>
        _cachedPluginManifests ??= [.. pluginContainers.SelectMany(container => container.GetPluginManifests())];

    private (List<PluginServiceCollection> pluginServiceCollections, PluginServiceCollection publicServicesOnlyCollection) InitializePlugins(IEnumerable<IPluginManifest> pluginManifests)
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
        HashSet<string> publicServiceAssemblies = [.. publicServiceTypeRegistry.GetAssemblyNames()];
        var publicServiceDescriptors = serviceCollection
            .Where(sd => sd.ServiceType.Assembly.FullName is string assemblyName && publicServiceAssemblies.Contains(assemblyName));
        return [.. publicServiceDescriptors];
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

        await DisposeProvidersAsync(providersToDispose).ConfigureAwait(false);
    }

    private (List<IServiceProvider> PluginServices, IServiceProvider PublicServices) SnapshotCurrentServiceProviders()
        => (_pluginServiceCollections.Select(collection => collection.ServiceProvider!).ToList(),
            _publicServicesOnlyCollection.ServiceProvider!);

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
            asyncDisposable.DisposeAsync().AsTask()
                .ConfigureAwait(false).GetAwaiter().GetResult();
            return;
        }

        if (provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static async ValueTask DisposeProvidersAsync(IEnumerable<IServiceProvider> providers)
    {
        foreach (IServiceProvider provider in providers)
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