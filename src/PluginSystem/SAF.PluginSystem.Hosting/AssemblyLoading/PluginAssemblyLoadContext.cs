// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.AssemblyLoading;

using Microsoft.Extensions.Logging;
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
                return HandleConflict(assemblyName, hostVersion!);

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
            // The runtime wraps exceptions thrown from Load in a FileLoadException; log here so the clear
            // diagnostic is visible regardless of how the caller surfaces the error.
            _logger.LogError(
                "Plugin requires shared assembly {AssemblyName} version {RequestedVersion}, which is not compatible " +
                "with the host-provided version {HostVersion}. Failing the plugin assembly load.",
                assemblyName.Name, requestedVersion, hostVersion);

            throw new SharedAssemblyVersionConflictException(assemblyName.Name!, requestedVersion, hostVersion);
        }

        var isolatedPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (isolatedPath is null)
        {
            // Nothing to isolate: the runtime falls back to the host version from the default context.
            _logger.LogWarning(
                "Plugin requires shared assembly {AssemblyName} version {RequestedVersion}, which is not compatible " +
                "with the host-provided version {HostVersion}, and ships no private copy. Falling back to the host " +
                "version; the plugin may fail at runtime.",
                assemblyName.Name, requestedVersion, hostVersion);

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
