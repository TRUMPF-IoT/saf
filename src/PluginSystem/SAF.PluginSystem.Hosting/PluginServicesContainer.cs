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
    : IPluginServicesContainer, IPluginServicesReloader, IAsyncDisposable
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
    private PluginServiceCollection _publicServicesOnlyCollection = CreatePublicServicesOnlyCollection();

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
        var publishResult = PublishProviders(pluginServiceCollections, publicServicesOnlyCollection, replaceExisting: true, cancellationToken);

        await DisposeProvidersAsync(publishResult.ProvidersToDispose).ConfigureAwait(false);

        if (publishResult.DeferredException is not null)
        {
            throw publishResult.DeferredException;
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
        var publishResult = PublishProviders(pluginServiceCollections, publicServicesOnlyCollection, replaceExisting: false);

        DisposeProviders(publishResult.ProvidersToDispose);

        if (publishResult.DeferredException is not null)
        {
            throw publishResult.DeferredException;
        }

        return publishResult.CurrentProviders;
    }

    private static PluginServiceCollection CreatePublicServicesOnlyCollection() =>
        new(new ServiceCollection(), []) { Name = "public services" };

    private List<IPluginManifest> GetPluginManifests() =>
        _cachedPluginManifests ??= [.. pluginContainers.SelectMany(container => container.GetPluginManifests())];

    private (List<PluginServiceCollection> pluginServiceCollections, PluginServiceCollection publicServicesOnlyCollection) InitializePlugins(IEnumerable<IPluginManifest> pluginManifests)
    {
        List<PluginServiceCollection> pluginServiceCollections = [];
        PluginServiceCollection publicServicesOnlyCollection = CreatePublicServicesOnlyCollection();

        try
        {
            foreach (var manifest in pluginManifests)
            {
                var pluginServices = new ServiceCollection();

                applicationServiceProvider.RedirectCommonServices(pluginServices);

                manifest.ConfigureServices(hostContext, pluginServices);

                pluginServiceCollections.Add(new PluginServiceCollection(pluginServices, CollectPublicServices(pluginServices))
                {
                    Name = manifest.GetType().Assembly.GetName().Name ?? string.Empty
                });
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
        List<NamedProvider> providersToDispose;

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

    private PublishProvidersResult PublishProviders(
        List<PluginServiceCollection> pluginServiceCollections,
        PluginServiceCollection publicServicesOnlyCollection,
        bool replaceExisting,
        CancellationToken cancellationToken = default)
    {
        lock (_syncPluginLoading)
        {
            if (_disposed)
            {
                return new(
                    SnapshotProviders(pluginServiceCollections, publicServicesOnlyCollection),
                    SnapshotCurrentServiceProviders(),
                    new ObjectDisposedException(nameof(PluginServicesContainer)));
            }

            if (replaceExisting && cancellationToken.IsCancellationRequested)
            {
                return new(
                    SnapshotProviders(pluginServiceCollections, publicServicesOnlyCollection),
                    SnapshotCurrentServiceProviders(),
                    new OperationCanceledException(cancellationToken));
            }

            List<NamedProvider> providersToDispose;

            if (replaceExisting || !_initialized)
            {
                providersToDispose = SnapshotProviders();
                _pluginServiceCollections = pluginServiceCollections;
                _publicServicesOnlyCollection = publicServicesOnlyCollection;
                _initialized = true;
            }
            else
            {
                providersToDispose = SnapshotProviders(pluginServiceCollections, publicServicesOnlyCollection);
            }

            return new(providersToDispose, SnapshotCurrentServiceProviders(), null);
        }
    }

    private List<NamedProvider> SnapshotProviders() =>
        SnapshotProviders(_pluginServiceCollections, _publicServicesOnlyCollection);

    // The built providers in load order, followed by the one of the public services.
    private static List<NamedProvider> SnapshotProviders(
        IEnumerable<PluginServiceCollection> pluginServiceCollections,
        PluginServiceCollection publicServicesOnlyCollection) =>
        [.. pluginServiceCollections
            .Append(publicServicesOnlyCollection)
            .Where(collection => collection.ServiceProvider is not null)
            .Select(collection => new NamedProvider(collection.Name, collection.ServiceProvider!))];

    /// <summary>
    /// Orders the providers for disposal: the reverse of the order the plug-ins were loaded in.
    /// </summary>
    /// <remarks>
    /// Microsoft's container disposes its singletons in the reverse order of their creation, too: a plug-in commonly
    /// uses the services of the plug-ins loaded before it, such as the messaging, and can then still release them in
    /// its own disposal. The provider of the public services only imports services from the others and goes first.
    /// </remarks>
    private static IEnumerable<NamedProvider> InDisposalOrder(IEnumerable<NamedProvider> providers) =>
        providers.Reverse();

    // Only gets providers that were built but never handed out: after a failed initialization, or when another
    // initialization or the disposal of this container came first. None of their services has been created, so unlike
    // in DisposeProvidersAsync, their disposal runs no service's Dispose that could fail.
    private static void DisposeProviders(IEnumerable<NamedProvider> providers)
    {
        foreach (NamedProvider provider in InDisposalOrder(providers))
        {
            DisposeProvider(provider.Provider);
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

    // A failing provider must not keep the others from being disposed. Within it, Microsoft's container stops at the
    // failing service, so the services it created before that one stay undisposed.
    private async ValueTask DisposeProvidersAsync(IEnumerable<NamedProvider> providers)
    {
        foreach (NamedProvider provider in InDisposalOrder(providers))
        {
            try
            {
                await DisposeProviderAsync(provider.Provider).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to dispose the service provider of {Plugin}. The services it created before the failing one were not disposed.",
                    provider.Name);
            }
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

    private sealed record PublishProvidersResult(
        List<NamedProvider> ProvidersToDispose,
        (List<IServiceProvider> PluginServices, IServiceProvider PublicServices) CurrentProviders,
        Exception? DeferredException);

    private readonly record struct NamedProvider(string Name, IServiceProvider Provider);
}
