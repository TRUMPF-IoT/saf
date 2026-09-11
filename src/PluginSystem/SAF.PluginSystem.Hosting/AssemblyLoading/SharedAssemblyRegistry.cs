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
using System.Runtime.Loader;

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
            Record(sharedAssemblies, assemblyName, AssemblyOrigin.Loaded);
        }

        foreach (var source in sharedAssemblySources)
        {
            RecordFromSource(sharedAssemblies, source);
        }

        try
        {
            var contractFullNames = publicServiceTypeRegistry.GetAssemblyNames();

            foreach (var contractFullName in contractFullNames)
            {
                try
                {
                    Record(sharedAssemblies, new AssemblyName(contractFullName), AssemblyOrigin.OnDisk);
                }
                catch (Exception ex) when (ex is FileLoadException or ArgumentException)
                {
                    logger.LogWarning(ex, "Ignoring malformed plugin contract assembly name {AssemblyFullName}.", contractFullName);
                }
            }
        }
        catch (Exception ex)
        {
            // IPublicServiceTypeRegistry is a public extension point: a throwing implementation must not
            // abort the whole shared set, it just contributes no contract assemblies.
            logger.LogWarning(ex, "Ignoring plugin contract assemblies because {PublicServiceTypeRegistryType} threw while listing them.", publicServiceTypeRegistry.GetType().Name);
        }

        return sharedAssemblies;
    }

    private void RecordFromSource(Dictionary<string, SharedAssemblyInfo> sharedAssemblies, ISharedAssemblySource source)
    {
        // ISharedAssemblySource is a public extension point: a throwing or misbehaving implementation must
        // not abort the whole shared set, it just contributes no assemblies.
        IEnumerable<AssemblyName>? assemblyNames;
        try
        {
            assemblyNames = source.GetSharedAssemblyNames();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ignoring shared assembly source {SharedAssemblySourceType} because it threw while listing its assemblies.", source.GetType().Name);
            return;
        }

        if (assemblyNames is null)
        {
            logger.LogWarning("Ignoring shared assembly source {SharedAssemblySourceType} because it returned null instead of a shared assembly name sequence.", source.GetType().Name);
            return;
        }

        foreach (var assemblyName in assemblyNames)
        {
            if (assemblyName is null)
            {
                logger.LogWarning("Ignoring a null assembly name returned by shared assembly source {SharedAssemblySourceType}.", source.GetType().Name);
                continue;
            }

            Record(sharedAssemblies, assemblyName, AssemblyOrigin.Loaded);
        }
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

    private void Record(Dictionary<string, SharedAssemblyInfo> sharedAssemblies, AssemblyName name, AssemblyOrigin origin)
    {
        if (name.Name is null)
        {
            return;
        }

        // A hand-written ISharedAssemblySource can return an AssemblyName built from just a simple name
        // (unlike typeof(T).Assembly.GetName(), which always carries a version); fall back to whatever
        // version is already loaded under that name instead of dropping the entry outright.
        var version = name.Version ?? ResolveVersionFromDefaultContext(name.Name);
        if (version is null)
        {
            logger.LogWarning(
                "Ignoring shared assembly {AssemblyName}: it has no version, and none could be derived from an " +
                "already-loaded assembly of the same simple name.", name.Name);
            return;
        }

        // An on-disk candidate (a file matching PluginContractsSearchPattern, read via
        // AssemblyName.GetAssemblyName) never overrides an assembly already recorded from a loaded one (SAF's
        // own implicit set, or a SharedAssemblySource<T> reporting typeof(T).Assembly.GetName()): a stale
        // copy sitting next to the host would otherwise win over the version actually bound in the default
        // context, and the registry would report that stale version as the host version to compare plugins
        // against.
        if (origin == AssemblyOrigin.OnDisk && sharedAssemblies.TryGetValue(name.Name, out var loaded))
        {
            if (loaded.Version != version)
            {
                logger.LogWarning(
                    "Ignoring on-disk version {OnDiskVersion} of shared assembly {AssemblyName}; keeping the already-loaded version {LoadedVersion}.",
                    version, name.Name, loaded.Version);
            }

            return;
        }

        sharedAssemblies[name.Name] = new SharedAssemblyInfo(version, SharedAssemblyInfo.NormalizeToken(name.GetPublicKeyToken()));

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Shared plugin assembly registered: {AssemblyName} {AssemblyVersion}", name.Name, version);
        }
    }

    private static Version? ResolveVersionFromDefaultContext(string simpleName)
        => AssemblyLoadContext.Default.Assemblies
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
            ?.GetName().Version;

    private enum AssemblyOrigin
    {
        Loaded,
        OnDisk,
    }
}
