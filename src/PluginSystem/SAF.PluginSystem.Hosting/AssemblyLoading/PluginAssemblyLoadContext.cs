// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.AssemblyLoading;

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;

internal sealed class PluginAssemblyLoadContext(
    ILoggerFactory loggerFactory,
    string pluginAssemblyPath,
    ISharedAssemblyResolver sharedAssemblyResolver,
    SharedAssemblyConflictBehavior conflictBehavior) : AssemblyLoadContext
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<PluginAssemblyLoadContext>();

    private readonly AssemblyDependencyResolver _resolver = new(pluginAssemblyPath);

    private readonly ConcurrentQueue<SharedAssemblyVersionConflictException> _conflicts = new();

    internal IReadOnlyCollection<SharedAssemblyVersionConflictException> Conflicts => _conflicts;

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        ArgumentNullException.ThrowIfNull(assemblyName);

        switch (sharedAssemblyResolver.Resolve(assemblyName, out var hostVersion))
        {
            case SharedAssemblyDecision.ShareFromDefault:
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("Share assembly {AssemblyFullName} from the default context.", assemblyName.FullName);
                }

                // Returning null defers resolution to the default context, which shares the single
                // instance of this assembly across the plugin boundary.
                return null;

            case SharedAssemblyDecision.Conflict:
                // ISharedAssemblyResolver is a public extension point: nothing enforces that a Conflict
                // decision actually carries a host version or that the request carries a simple name (both
                // are only documented expectations). Forcing either with `!` here would let a misbehaving
                // resolver construct a SharedAssemblyVersionConflictException with null fields instead.
                if (hostVersion is null || assemblyName.Name is null)
                {
                    _logger.LogError(
                        "Shared assembly resolver {SharedAssemblyResolverType} reported a conflict for " +
                        "{AssemblyFullName} without a host version and/or simple name; the response cannot be " +
                        "used. Loading in isolation instead.",
                        sharedAssemblyResolver.GetType().Name, assemblyName.FullName);
                    return LoadIsolated(assemblyName);
                }

                return HandleConflict(assemblyName, hostVersion);

            default:
                return LoadIsolated(assemblyName);
        }
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath != null ? LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
    }

    private Assembly? HandleConflict(AssemblyName assemblyName, Version hostVersion)
    {
        var requestedVersion = assemblyName.Version ?? new Version(0, 0, 0, 0);

        if (conflictBehavior == SharedAssemblyConflictBehavior.Fail)
        {
            // Logged unconditionally: Load may run again later for a member touched only during plugin
            // execution, after the container has already stopped checking Conflicts. Returning null
            // instead of throwing lets the default context bind the host version; the container turns
            // the queued conflict into a hard failure once loading completes.
            _logger.LogError(
                "Plugin requires shared assembly {AssemblyName} version {RequestedVersion}, which is not compatible " +
                "with the host-provided version {HostVersion}. Failing the plugin assembly load.",
                assemblyName.Name, requestedVersion, hostVersion);

            // Name is non-null here: Load only reaches HandleConflict once it has confirmed that itself.
            _conflicts.Enqueue(new SharedAssemblyVersionConflictException(assemblyName.Name!, requestedVersion, hostVersion));
            return null;
        }

        var isolatedPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (isolatedPath is null)
        {
            // The default context's binder only rolls a reference forward to a HIGHER already-bound
            // version; it never binds one down. Below this point, "falling back to the host version"
            // is only true when the host is higher (a disallowed major roll-forward) - when the host is
            // actually lower, the bind itself fails and this plugin will not load.
            if (hostVersion > requestedVersion)
            {
                _logger.LogWarning(
                    "Plugin requires shared assembly {AssemblyName} version {RequestedVersion}, which is not compatible " +
                    "with the host-provided version {HostVersion}, and ships no private copy. Falling back to the host " +
                    "version; the plugin may fail at runtime.",
                    assemblyName.Name, requestedVersion, hostVersion);
            }
            else
            {
                _logger.LogWarning(
                    "Plugin requires shared assembly {AssemblyName} version {RequestedVersion}, which is not compatible " +
                    "with the host-provided version {HostVersion}, and ships no private copy. The default context " +
                    "cannot bind the lower host version to this request; loading this plugin will fail.",
                    assemblyName.Name, requestedVersion, hostVersion);
            }

            return null;
        }

        _logger.LogWarning(
            "Plugin requires shared assembly {AssemblyName} version {RequestedVersion}, which is not compatible with " +
            "the host-provided version {HostVersion}. Loading the plugin's private copy in isolation; types of this " +
            "assembly will not be compatible across the plugin boundary.",
            assemblyName.Name, requestedVersion, hostVersion);

        return LoadFromAssemblyPath(isolatedPath);
    }

    private Assembly? LoadIsolated(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Load assembly {AssemblyFullName} from path {AssemblyPath} in isolation.", assemblyName.FullName, assemblyPath);
            }

            return LoadFromAssemblyPath(assemblyPath);
        }

        // The assembly is not part of the plugin's own dependencies (e.g. a framework assembly). Defer to
        // the default context.
        return null;
    }
}
