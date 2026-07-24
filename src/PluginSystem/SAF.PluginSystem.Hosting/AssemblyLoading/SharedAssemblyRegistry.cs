// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.AssemblyLoading;

using Contracts;
using Microsoft.Extensions.Logging;
using System.Reflection;

/// <inheritdoc />
/// <remarks>
/// The shared set is the transitive closure of the plugin contract assemblies (as reported by
/// <see cref="IPublicServiceTypeRegistry"/>) plus the SAF plugin hosting contracts assembly. The closure
/// is derived from assembly metadata (<see cref="Assembly.GetReferencedAssemblies"/>) via an
/// <see cref="IAssemblyGraphProvider"/>; there is no dependency on the deps.json format. Every assembly
/// the host can resolve while walking the closure is recorded, framework assemblies included. Recording a
/// framework assembly is harmless: the resolver shares it from the default context, which is what the
/// runtime does for framework assemblies anyway.
/// </remarks>
internal sealed class SharedAssemblyRegistry(
    ILogger<SharedAssemblyRegistry> logger,
    IPublicServiceTypeRegistry publicServiceTypeRegistry,
    IAssemblyGraphProvider assemblyGraphProvider)
    : ISharedAssemblyRegistry
{
    private readonly Dictionary<string, SharedAssemblyInfo> _sharedAssemblies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _syncInitialization = new();
    private bool _initialized;

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
        return _sharedAssemblies;
    }

    private void EnsureInitialized()
    {
        lock (_syncInitialization)
        {
            if (_initialized)
            {
                return;
            }

            BuildClosure();
            _initialized = true;

            logger.LogInformation(
                "Computed shared plugin assembly set with {SharedAssemblyCount} assemblies.",
                _sharedAssemblies.Count);
        }
    }

    private void BuildClosure()
    {
        _sharedAssemblies.Clear();

        var seeds = CollectSeeds();
        var seedNames = new HashSet<string>(seeds.Select(s => s.Name!), StringComparer.OrdinalIgnoreCase);

        var queue = new Queue<AssemblyName>(seeds);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (queue.Count > 0)
        {
            var assemblyName = queue.Dequeue();

            if (assemblyName.Name is null || !visited.Add(assemblyName.Name))
            {
                continue;
            }

            var node = assemblyGraphProvider.TryResolve(assemblyName);
            if (node is null)
            {
                LogUnresolved(assemblyName, isSeed: seedNames.Contains(assemblyName.Name));
                continue;
            }

            Record(node.Name);

            foreach (var reference in node.ReferencedAssemblies)
            {
                if (reference.Name is not null && !visited.Contains(reference.Name))
                {
                    queue.Enqueue(reference);
                }
            }
        }
    }

    private List<AssemblyName> CollectSeeds()
    {
        // Plugin hosting contracts: their types (IPluginManifest, IServiceCollection, ...) always cross the boundary.
        var seeds = new List<AssemblyName> { typeof(IPluginManifest).Assembly.GetName() };

        foreach (var contractFullName in publicServiceTypeRegistry.GetAssemblyNames())
        {
            try
            {
                seeds.Add(new AssemblyName(contractFullName));
            }
            catch (Exception ex) when (ex is FileLoadException or ArgumentException)
            {
                logger.LogWarning(ex, "Ignoring malformed plugin contract assembly name {AssemblyFullName}.", contractFullName);
            }
        }

        return seeds;
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

    private void LogUnresolved(AssemblyName assemblyName, bool isSeed)
    {
        if (isSeed)
        {
            logger.LogWarning(
                "Plugin contract assembly {AssemblyFullName} could not be resolved on the host and will not be shared. " +
                "Types of this assembly cannot cross the plugin boundary.",
                assemblyName.FullName);
        }
        else if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Transitive contract dependency {AssemblyFullName} is not provided by the host; it will not be shared.",
                assemblyName.FullName);
        }
    }
}
