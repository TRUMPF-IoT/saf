// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.IO.Abstractions;

/// <inheritdoc />
public class PluginAssemblyFolderContainer(
    ILoggerFactory loggerFactory,
    IPluginManifestLoader manifestLoader,
    PluginAssemblyFolderSearchOptions options,
    IEnumerable<IPluginAssemblyValidator> assemblyValidators,
    IFileSystem fileSystem)
    : IPluginAssemblyContainer
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<PluginAssemblyFolderContainer>();
    private readonly IPluginManifestLoader _manifestLoader = manifestLoader;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly IReadOnlyList<IPluginAssemblyValidator> _assemblyValidators = assemblyValidators.ToList();
    private IReadOnlyList<IPluginManifest>? _cachedManifests;
    private readonly Lock _cacheLock = new();

    private PluginAssemblyFolderSearchOptions SearchOptions { get; } = options;

    /// <summary>
    /// Returns all <see cref="IPluginManifest"/> instances discovered in the configured folder.
    /// Results are cached after the first call so that assemblies are loaded only once.
    /// </summary>
    public IEnumerable<IPluginManifest> GetPluginManifests()
    {
        lock (_cacheLock)
        {
            if (_cachedManifests is not null)
            {
                return _cachedManifests;
            }

            var pluginAssemblyPaths = GetPluginAssemblyPaths();
            _cachedManifests = [.. LoadManifests(pluginAssemblyPaths)];
            return _cachedManifests;
        }
    }

    private List<string> GetPluginAssemblyPaths()
        => SearchDirectoryForMatchingFiles(SearchOptions.SearchRootPath);

    private List<string> SearchDirectoryForMatchingFiles(string directory)
    {
        if (!_fileSystem.Directory.Exists(directory))
        {
            _logger.LogWarning("Configured plugin directory {SearchDirectory} not found. No plugins will be loaded.", directory);
            return [];
        }

        _logger.LogDebug("Searching for matching plugin assemblies in directory {SearchDirectory}{Recursive}",
            directory, SearchOptions.Recursive ? " recursive" : string.Empty);

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddIncludePatterns(SearchOptions.IncludePatterns.Split(';'));
        matcher.AddExcludePatterns(SearchOptions.ExcludePatterns.Split(';'));

        var result = matcher.GetResultsInFullPath(directory).ToList();
        _logger.LogDebug("Found {MatchingAssemblyCount} matching assemblies", result.Count);

        if (SearchOptions.Recursive)
        {
            foreach (var subDir in _fileSystem.Directory.GetDirectories(directory))
            {
                result.AddRange(SearchDirectoryForMatchingFiles(subDir));
            }
        }

        return result;
    }

    private List<IPluginManifest> LoadManifests(List<string> pluginAssemblyPaths)
    {
        List<IPluginManifest> manifests = [];

        foreach (var pluginAssemblyPath in pluginAssemblyPaths)
        {
            if (!TryValidateAssembly(pluginAssemblyPath, out var rejectionReason))
            {
                _logger.LogWarning("Skip plugin assembly {PluginAssemblyPath}: {Reason}", pluginAssemblyPath, rejectionReason);
                continue;
            }

            _logger.LogDebug("Create AssemblyLoadContext for {PluginAssemblyPath}", pluginAssemblyPath);

            var isInBaseDirectory = string.Compare(
                _fileSystem.Path.GetDirectoryName(AppContext.BaseDirectory),
                _fileSystem.Path.GetDirectoryName(pluginAssemblyPath),
                StringComparison.OrdinalIgnoreCase) == 0;

            var pluginLoadContext = isInBaseDirectory
                ? AssemblyLoadContext.Default
                : new PluginAssemblyLoadContext(loggerFactory, pluginAssemblyPath, _fileSystem);

            try
            {
                var assembly = pluginLoadContext.LoadFromAssemblyPath(pluginAssemblyPath);
                var manifest = _manifestLoader.LoadPluginManifest(assembly);

                if (manifest == null)
                {
                    _logger.LogWarning("Can't find manifest in {Assembly} from {AssemblyLocation}, skipping assembly.", assembly, assembly.Location);
                    continue;
                }

                _logger.LogDebug("Found manifest in {Assembly} from {AssemblyLocation}", assembly, assembly.Location);
                manifests.Add(manifest);
            }
            catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or FileNotFoundException or ReflectionTypeLoadException or TypeLoadException)
            {
                _logger.LogError(ex, "Failed to load plugin manifest from {PluginAssemblyPath}, skipping assembly.", pluginAssemblyPath);
            }
        }

        return manifests;
    }

    private bool TryValidateAssembly(string pluginAssemblyPath, out string rejectionReason)
    {
        rejectionReason = string.Empty;

        AssemblyName assemblyName;
        try
        {
            assemblyName = AssemblyName.GetAssemblyName(pluginAssemblyPath);
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or FileNotFoundException)
        {
            rejectionReason = $"metadata could not be read ({ex.GetType().Name})";
            return false;
        }

        var validationContext = new PluginAssemblyValidationContext(pluginAssemblyPath, assemblyName);

        foreach (var validator in _assemblyValidators)
        {
            var result = validator.Validate(validationContext);
            if (result.IsAccepted)
            {
                continue;
            }

            rejectionReason = string.IsNullOrWhiteSpace(result.Reason)
                ? $"rejected by validator {validator.GetType().Name}"
                : result.Reason;
            return false;
        }

        return true;
    }
}
