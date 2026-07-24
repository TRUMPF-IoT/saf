// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.AssemblyLoading;

using Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using System.Reflection;

/// <inheritdoc />
/// <remarks>
/// The shared set is explicit, not computed from a dependency scan: it is the union of the assemblies SAF
/// implicitly forces across the plugin boundary (see <see cref="CollectImplicitlySharedAssemblies"/>) and
/// the plugin contract assemblies configured through
/// <see cref="PluginSystemOptions.PluginContractsSearchPattern"/> (reported by
/// <see cref="IPublicServiceTypeRegistry"/>). Consumers must therefore configure any additional dependency
/// whose types cross the boundary; anything not in the set loads isolated per plugin.
/// </remarks>
internal sealed class SharedAssemblyRegistry(
    ILogger<SharedAssemblyRegistry> logger,
    IPublicServiceTypeRegistry publicServiceTypeRegistry)
    : ISharedAssemblyRegistry
{
    private readonly Dictionary<string, SharedAssemblyInfo> _sharedAssemblies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _syncInitialization = new();
    private volatile bool _initialized;

    /// <inheritdoc />
    public bool TryGetSharedAssembly(string simpleName, out SharedAssemblyInfo info)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(simpleName);

        EnsureInitialized();
        return _sharedAssemblies.TryGetValue(simpleName, out info);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, SharedAssemblyInfo> GetSharedAssemblies()
    {
        EnsureInitialized();
        return new Dictionary<string, SharedAssemblyInfo>(_sharedAssemblies, StringComparer.OrdinalIgnoreCase);
    }

    private void EnsureInitialized()
    {
        // Double-checked locking: once built, the volatile read makes lookups lock-free on the assembly-load
        // hot path. The volatile write publishes the fully populated set with release semantics.
        if (_initialized)
        {
            return;
        }

        lock (_syncInitialization)
        {
            if (_initialized)
            {
                return;
            }

            BuildSharedSet();
            _initialized = true;

            logger.LogInformation(
                "Computed shared plugin assembly set with {SharedAssemblyCount} assemblies.",
                _sharedAssemblies.Count);
        }
    }

    private void BuildSharedSet()
    {
        _sharedAssemblies.Clear();

        foreach (var assemblyName in CollectImplicitlySharedAssemblies())
        {
            Record(assemblyName);
        }

        foreach (var contractFullName in publicServiceTypeRegistry.GetAssemblyNames())
        {
            try
            {
                Record(new AssemblyName(contractFullName));
            }
            catch (Exception ex) when (ex is FileLoadException or ArgumentException)
            {
                logger.LogWarning(ex, "Ignoring malformed plugin contract assembly name {AssemblyFullName}.", contractFullName);
            }
        }
    }

    private static IEnumerable<AssemblyName> CollectImplicitlySharedAssemblies()
    {
        // Everything SAF forces across the boundary: the hosting contracts (IPluginManifest,
        // IPluginSystemHostContext, IHostServiceForwarder, IPluginServiceProvider, ...) plus the abstraction
        // assemblies of the common services RedirectCommonServices injects into every plugin container.
        yield return typeof(IPluginManifest).Assembly.GetName();       // SAF.PluginSystem.Hosting.Contracts
        yield return typeof(IServiceCollection).Assembly.GetName();    // Microsoft.Extensions.DependencyInjection.Abstractions
        yield return typeof(IConfiguration).Assembly.GetName();        // Microsoft.Extensions.Configuration.Abstractions
        yield return typeof(ILoggerFactory).Assembly.GetName();        // Microsoft.Extensions.Logging.Abstractions
        yield return typeof(IFileSystem).Assembly.GetName();           // System.IO.Abstractions
    }

    private void Record(AssemblyName name)
    {
        if (name.Name is null || name.Version is null)
        {
            return;
        }

        _sharedAssemblies[name.Name] = new SharedAssemblyInfo(name.Version, name.GetPublicKeyToken());

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Shared plugin assembly registered: {AssemblyName} {AssemblyVersion}", name.Name, name.Version);
        }
    }
}
