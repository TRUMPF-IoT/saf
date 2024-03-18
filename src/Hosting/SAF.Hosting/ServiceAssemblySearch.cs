// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;

using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using Abstractions;

internal class ServiceAssemblySearch(ILogger<ServiceAssemblySearch> logger, IOptions<ServiceAssemblySearchOptions> searchOptions) : IServiceAssemblySearch
{
    private readonly ServiceAssemblySearchOptions _options = searchOptions.Value;

    public IEnumerable<IServiceAssemblyManifest> LoadServiceAssemblyManifests()
    {
        var serviceAssemblies = SearchServiceAssemblies(_options.BasePath, _options.SearchPath, _options.SearchFilenamePattern);
        return LoadServiceAssemblyManifests(serviceAssemblies);
    }

    private List<IServiceAssemblyManifest> LoadServiceAssemblyManifests(IList<string> assemblies)
    {
        var manifests = new List<IServiceAssemblyManifest>();

        foreach (var assembly in assemblies)
        {
            var loadedAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assembly);
            var versionInfo = FileVersionInfo.GetVersionInfo(loadedAssembly.Location);

            logger.LogInformation("Loading assembly: {assembly}, FileVersion: {fileVersion}, ProductVersion: {productVersion}, Version: {version}",
                    assembly, versionInfo.FileVersion, versionInfo.ProductVersion, loadedAssembly.GetName().Version);

            var assemblyManifests = loadedAssembly.GetExportedTypes().Where(t => t.IsClass && typeof(IServiceAssemblyManifest).IsAssignableFrom(t)).ToList();

            if (assemblyManifests.Count != 1)
            {
                logger.LogError("Assembly {assemblyFullName} skipped: a valid assembly should contain exactly one public manifest, found {manifestsFound}.",
                    loadedAssembly.FullName, assemblyManifests.Count);
                continue;
            }

            if (Activator.CreateInstance(assemblyManifests[0]) is not IServiceAssemblyManifest manifest)
            {
                logger.LogError("Assembly {assemblyFullName} skipped: failed to create manifest instance.", loadedAssembly.FullName);
                continue;
            }
            manifests.Add(manifest);
        }

        logger.LogInformation("Loaded {assembliesLoadedCount} assemblies.", manifests.Count);

        return manifests;
    }

    /// <summary>
    /// Search SAF service assemblies
    /// </summary>
    /// <param name="basePath">Base path where search starts</param>
    /// <param name="searchPath">Search path (glob pattern). The ';' character is used as delimiter for multiple patterns and '|' as prefix for exclusions</param>
    /// <param name="fileNameFilterRegEx">Filter regex that each filename must satisfy (if in doubt use ".*")</param>
    /// <returns></returns>
    private List<string> SearchServiceAssemblies(string basePath, string searchPath, string fileNameFilterRegEx)
    {
        // This function probably should be moved somewhere else
        if (basePath == null) throw new ArgumentNullException(nameof(basePath));
        if (searchPath == null) throw new ArgumentNullException(nameof(searchPath));
        if (fileNameFilterRegEx == null) throw new ArgumentNullException(nameof(fileNameFilterRegEx));

        logger.LogInformation("Searching SAF service assemblies using BasePath: {basePath}, SearchPath: {searchPath}, SearchFilenamePattern: {searchFilenamePattern}",
            _options.BasePath, _options.SearchPath, _options.SearchFilenamePattern);

        // Use matcher to find service  assemblies
        var serviceMatcher = new Matcher();
        foreach (var pattern in searchPath.Split(';'))
        {
            if (pattern.StartsWith('|')) serviceMatcher.AddExclude(pattern[1..]);
            else serviceMatcher.AddInclude(pattern);
        }

        var results = serviceMatcher.GetResultsInFullPath(basePath).ToList();

        // Filter assemblies using RegEx
        var serviceAssemblyNameRegEx = new Regex(fileNameFilterRegEx, RegexOptions.IgnoreCase, Timeout.InfiniteTimeSpan);
        results = results.Where(assembly => serviceAssemblyNameRegEx.IsMatch(Path.GetFileName(assembly))).ToList();

        logger.LogInformation("Found {serviceAssemblyCount} possible SAF service assemblies [{serviceAssemblies}]",
            results.Count, string.Join(", ", results.Select(Path.GetFileName)));

        return results;
    }
}