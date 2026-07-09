// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace SAF.PluginSystem.Hosting;

using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using System.Reflection;
using System.Runtime.Loader;

public class PluginAssemblyLoadContext(ILoggerFactory loggerFactory, string pluginAssemblyPath, IFileSystem fileSystem) : AssemblyLoadContext
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<PluginAssemblyLoadContext>();

    private readonly AssemblyDependencyResolver _resolver = new(pluginAssemblyPath);

    private readonly string _defaultApplicationDirectory = fileSystem.Path.GetDirectoryName(AppContext.BaseDirectory) ?? string.Empty;
    private readonly HashSet<string> _defaultApplicationAssemblies = new();
    private readonly Lock _syncDefaultApplicationAssemblies = new();

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        ArgumentNullException.ThrowIfNull(assemblyName);

        if (IsLoadedInDefaultContext(assemblyName))
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Load assembly {AssemblyFullName} from default context", assemblyName.FullName);
            }

            return null;
        }

        if (IsLoadableInDefaultContext(assemblyName))
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Load assembly {AssemblyFullName} from default context because the same assembly is available in the default application directory", assemblyName.FullName);
            }

            return null;
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        _logger.LogTrace("Load assembly {AssemblyFullName} from path {AssemblyPath}", assemblyName.FullName, assemblyPath);

        return assemblyPath != null ? LoadFromAssemblyPath(assemblyPath) : null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath != null ? LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
    }

    private static bool IsLoadedInDefaultContext(AssemblyName assemblyName)
        => Default.Assemblies.Any(a => a.FullName == assemblyName.FullName);

    private bool IsLoadableInDefaultContext(AssemblyName assemblyName)
    {
        // check whether the exact same assembly is available in the _defaultApplicationDirectory,
        // if so load it in the default context to avoid loading the same assembly twice.
        // this is required to ensure that types from the default context and the plugin context are compatible with each other,
        // e.g. for common interfaces and in particular their dependencies.
        // this makes referencing shared assemblies more convenient, as they can simply be placed in the default application directory
        // and will be automatically shared between the default context and all plugin contexts.

        InitializeDefaultApplicationAssemblies();
        return _defaultApplicationAssemblies.Contains(assemblyName.FullName);
    }

    private void InitializeDefaultApplicationAssemblies()
    {
        lock (_syncDefaultApplicationAssemblies)
        {
            if (_defaultApplicationAssemblies.Count > 0)
            {
                return;
            }

            var assemblyFiles = fileSystem.Directory.EnumerateFiles(_defaultApplicationDirectory, "*.dll");
            foreach (var assemblyFilePath in assemblyFiles)
            {
                try
                {
                    var assemblyName = AssemblyName.GetAssemblyName(assemblyFilePath);
                    _defaultApplicationAssemblies.Add(assemblyName.FullName!);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get assembly name for file {FilePath} in default application directory", assemblyFilePath);
                }
            }
        }
    }
}