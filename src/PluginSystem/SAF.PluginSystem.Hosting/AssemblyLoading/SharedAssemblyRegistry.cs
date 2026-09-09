// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.AssemblyLoading;

using Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.IO.Abstractions;
using System.Reflection;

/// <inheritdoc />
/// <remarks>
/// The shared set is explicit, not computed from a dependency scan: it is the union of the assemblies SAF
/// implicitly forces across the plugin boundary (see <see cref="CollectImplicitlySharedAssemblies"/>), the
/// assemblies contributed by <see cref="ISharedAssemblySource"/> registrations, and the plugin contract
/// assemblies configured through <see cref="PluginSystemOptions.PluginContractsSearchPattern"/>. Anything
/// not in the set loads isolated per plugin.
/// </remarks>
internal sealed class SharedAssemblyRegistry(
    ILogger<SharedAssemblyRegistry> logger,
    IPublicServiceTypeRegistry publicServiceTypeRegistry,
    IEnumerable<ISharedAssemblySource> sharedAssemblySources)
    : ISharedAssemblyRegistry
{
    private readonly Lock _syncInitialization = new();
    private volatile IReadOnlyDictionary<string, SharedAssemblyInfo>? _sharedAssemblies;

    /// <inheritdoc />
    public bool TryGetSharedAssembly(string simpleName, out SharedAssemblyInfo info)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(simpleName);

        return EnsureInitialized().TryGetValue(simpleName, out info);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, SharedAssemblyInfo> GetSharedAssemblies()
        => new Dictionary<string, SharedAssemblyInfo>(EnsureInitialized(), StringComparer.OrdinalIgnoreCase);

    private IReadOnlyDictionary<string, SharedAssemblyInfo> EnsureInitialized()
    {
        // Double-checked locking: once published, the volatile read makes lookups lock-free on the
        // assembly-load hot path. Publication is a single reference swap of a fully built dictionary, so a
        // reentrant rebuild (BuildSharedSet is called again on the same thread before the first call
        // returns, e.g. because building it triggers a managed load that re-enters this method) can never
        // observe or mutate a partially-filled shared set - each call only ever writes to its own local
        // dictionary, and the last publish wins.
        var sharedAssemblies = _sharedAssemblies;
        if (sharedAssemblies is not null)
        {
            return sharedAssemblies;
        }

        lock (_syncInitialization)
        {
            sharedAssemblies = _sharedAssemblies;
            if (sharedAssemblies is not null)
            {
                return sharedAssemblies;
            }

            sharedAssemblies = BuildSharedSet();
            _sharedAssemblies = sharedAssemblies;

            logger.LogInformation(
                "Computed shared plugin assembly set with {SharedAssemblyCount} assemblies.",
                sharedAssemblies.Count);

            return sharedAssemblies;
        }
    }

    private Dictionary<string, SharedAssemblyInfo> BuildSharedSet()
    {
        var sharedAssemblies = new Dictionary<string, SharedAssemblyInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var assemblyName in CollectImplicitlySharedAssemblies())
        {
            Record(sharedAssemblies, assemblyName);
        }

        foreach (var assemblyName in sharedAssemblySources.SelectMany(source => source.GetSharedAssemblyNames()))
        {
            Record(sharedAssemblies, assemblyName);
        }

        foreach (var contractFullName in publicServiceTypeRegistry.GetAssemblyNames())
        {
            try
            {
                Record(sharedAssemblies, new AssemblyName(contractFullName));
            }
            catch (Exception ex) when (ex is FileLoadException or ArgumentException)
            {
                logger.LogWarning(ex, "Ignoring malformed plugin contract assembly name {AssemblyFullName}.", contractFullName);
            }
        }

        return sharedAssemblies;
    }

    private static IEnumerable<AssemblyName> CollectImplicitlySharedAssemblies()
    {
        // Everything SAF forces across the boundary: the hosting contracts, the abstraction assemblies of
        // the common services RedirectCommonServices injects, and the assemblies those expose on their own
        // public surface (Options via the Contracts helpers, Primitives via IConfiguration.GetReloadToken).
        yield return typeof(IPluginManifest).Assembly.GetName();       // SAF.PluginSystem.Hosting.Contracts
        yield return typeof(IServiceCollection).Assembly.GetName();    // Microsoft.Extensions.DependencyInjection.Abstractions
        yield return typeof(IConfiguration).Assembly.GetName();        // Microsoft.Extensions.Configuration.Abstractions
        yield return typeof(ILoggerFactory).Assembly.GetName();        // Microsoft.Extensions.Logging.Abstractions
        yield return typeof(IFileSystem).Assembly.GetName();           // System.IO.Abstractions
        yield return typeof(IOptions<>).Assembly.GetName();            // Microsoft.Extensions.Options
        yield return typeof(IChangeToken).Assembly.GetName();          // Microsoft.Extensions.Primitives
    }

    private void Record(Dictionary<string, SharedAssemblyInfo> sharedAssemblies, AssemblyName name)
    {
        if (name.Name is null || name.Version is null)
        {
            return;
        }

        sharedAssemblies[name.Name] = new SharedAssemblyInfo(name.Version, name.GetPublicKeyToken());

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Shared plugin assembly registered: {AssemblyName} {AssemblyVersion}", name.Name, name.Version);
        }
    }
}
