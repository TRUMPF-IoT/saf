// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Runtime.Loader;
using System.IO.Abstractions;

/// <inheritdoc />
public class PluginAssemblyFolderContainer(
    ILoggerFactory loggerFactory,
    IPluginManifestLoader manifestLoader,
    PluginAssemblyFolderSearchOptions options,
    IFileSystem fileSystem)
    : IPluginAssemblyContainer
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<PluginAssemblyFolderContainer>();
    private readonly IPluginManifestLoader _manifestLoader = manifestLoader;
    private readonly IFileSystem _fileSystem = fileSystem;

    private PluginAssemblyFolderSearchOptions SearchOptions { get; } = options;

    public IEnumerable<IPluginManifest> GetPluginManifests()
    {
        var pluginAssemblyPaths = GetPluginAssemblyPaths();
        return LoadManifests(pluginAssemblyPaths);
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

    private IEnumerable<IPluginManifest> LoadManifests(List<string> pluginAssemblyPaths)
    {
        foreach (var pluginAssemblyPath in pluginAssemblyPaths)
        {
            _logger.LogDebug("Create AssemblyLoadContext for {PluginAssemblyPath}", pluginAssemblyPath);

            var isInBaseDirectory = string.Compare(
                _fileSystem.Path.GetDirectoryName(AppContext.BaseDirectory),
                _fileSystem.Path.GetDirectoryName(pluginAssemblyPath),
                StringComparison.OrdinalIgnoreCase) == 0;

            var pluginLoadContext = isInBaseDirectory
                ? AssemblyLoadContext.Default
                : new PluginAssemblyLoadContext(loggerFactory, pluginAssemblyPath, _fileSystem);

            var assembly = pluginLoadContext.LoadFromAssemblyPath(pluginAssemblyPath);
            var manifest = _manifestLoader.LoadPluginManifest(assembly);

            if (manifest == null)
            {
                _logger.LogWarning("Can't find manifest in {Assembly} from {AssemblyLocation}, skipping assembly.", assembly, assembly.Location);
                continue;
            }

            _logger.LogDebug("Found manifest in {Assembly} from {AssemblyLocation}", assembly, assembly.Location);

            yield return manifest;
        }
    }
}