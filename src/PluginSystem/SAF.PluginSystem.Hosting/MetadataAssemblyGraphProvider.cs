// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using System.Reflection;

/// <summary>
/// Default <see cref="IAssemblyGraphProvider"/> that inspects assembly metadata through a
/// <see cref="MetadataLoadContext"/>. It reads assembly identities and references <b>without loading the
/// assemblies into an executable context</b>, so building the shared-assembly closure does not eagerly
/// load (and pin) assemblies in the default context. Shared assemblies are loaded into the default
/// context lazily, only when a plugin actually requests them.
/// </summary>
/// <remarks>
/// The resolver is seeded with the assemblies of the application base directory and of the runtime
/// directory. The runtime directory is required so that a core assembly (defining <see cref="object"/>)
/// is available, which <see cref="MetadataLoadContext"/> mandates. Assembly identities are resolved by
/// simple name against these paths, which mirrors the version the default context will ultimately serve.
/// </remarks>
internal sealed class MetadataAssemblyGraphProvider : IAssemblyGraphProvider, IDisposable
{
    private readonly ILogger<MetadataAssemblyGraphProvider> _logger;
    private readonly MetadataLoadContext _metadataLoadContext;

    public MetadataAssemblyGraphProvider(ILogger<MetadataAssemblyGraphProvider> logger, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _logger = logger;

        var assemblyPaths = CollectAssemblyPaths(fileSystem);
        _metadataLoadContext = new MetadataLoadContext(new PathAssemblyResolver(assemblyPaths));
    }

    /// <inheritdoc />
    public AssemblyGraphNode? TryResolve(AssemblyName assemblyName)
    {
        ArgumentNullException.ThrowIfNull(assemblyName);

        try
        {
            var assembly = _metadataLoadContext.LoadFromAssemblyName(assemblyName);
            return new AssemblyGraphNode(assembly.GetName(), assembly.GetReferencedAssemblies());
        }
        catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
        {
            return null;
        }
    }

    public void Dispose() => _metadataLoadContext.Dispose();

    private static IReadOnlyList<string> CollectAssemblyPaths(IFileSystem fileSystem)
    {
        // Later entries win over earlier ones for the same simple name, so enumerate the runtime directory
        // first and the application base directory last: app-deployed assemblies take precedence, which is
        // exactly what the default context resolves to as well.
        var pathsBySimpleName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        AddAssemblies(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory());
        AddAssemblies(AppContext.BaseDirectory);

        return [.. pathsBySimpleName.Values];

        void AddAssemblies(string? directory)
        {
            if (string.IsNullOrEmpty(directory) || !fileSystem.Directory.Exists(directory))
            {
                return;
            }

            foreach (var file in fileSystem.Directory.EnumerateFiles(directory, "*.dll"))
            {
                pathsBySimpleName[fileSystem.Path.GetFileNameWithoutExtension(file)] = file;
            }
        }
    }
}
