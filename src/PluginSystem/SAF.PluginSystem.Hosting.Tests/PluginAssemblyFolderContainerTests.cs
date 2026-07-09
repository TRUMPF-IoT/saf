// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace SAF.PluginSystem.Hosting.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using Testably.Abstractions;

public class PluginAssemblyFolderContainerTests
{
    private readonly RealFileSystem _fileSystem;
    private readonly string _testPluginsPath;
    private readonly NullLoggerFactory _loggerFactory;
    private readonly PluginManifestLoader _manifestLoader;

    public PluginAssemblyFolderContainerTests()
    {
        // The tests enumerate real plugin assemblies on disk and load them via reflection /
        // AssemblyLoadContext, which always read from the real file system, so a mock cannot be used.
        _fileSystem = new RealFileSystem();
        _testPluginsPath = Path.Combine(AppContext.BaseDirectory, "test-plugins");
        _loggerFactory = NullLoggerFactory.Instance;
        _manifestLoader = new PluginManifestLoader();
    }

    [Fact]
    public void GetPluginAssemblyPaths_ConsidersSubdirectories()
    {
        // Arrange
        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = _testPluginsPath,
            IncludePatterns = "*.txt",
            ExcludePatterns = "*.exclude.*",
            Recursive = true
        };
        var container = new PluginAssemblyFolderContainer(NullLoggerFactory.Instance, new PluginManifestLoader(), options, _fileSystem);

        // Act
        var searchMethod = container.GetType().GetMethod("GetPluginAssemblyPaths", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var result = (searchMethod.Invoke(container, null) as List<string>)!;

        // Assert
        Assert.Equal(2, result.Count);
        Assert.True(result.TrueForAll(f => !f.Contains("exclude")));
        Assert.NotNull(result.SingleOrDefault(f => f.Contains("root.include.txt")));
        Assert.NotNull(result.SingleOrDefault(f => f.Contains("subdir.include.txt")));
    }

    [Fact]
    public void GetPluginAssemblyPaths_DoesNotConsiderSubdirectories()
    {
        // Arrange
        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = _testPluginsPath,
            IncludePatterns = "*.txt",
            ExcludePatterns = "*.exclude.*",
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, _manifestLoader, options, _fileSystem);

        // Act
        var searchMethod = container.GetType().GetMethod("GetPluginAssemblyPaths", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var result = (searchMethod.Invoke(container, null) as List<string>)!;

        // Assert
        Assert.Single(result);
        Assert.DoesNotContain("exclude", result[0]);
        Assert.Contains("root.include.txt", result[0]);
    }

    [Fact]
    public void GetPluginAssemblyPaths_ReturnsNoResult_WhenSearchPathNotFound()
    {
        // Arrange
        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = Path.Combine(AppContext.BaseDirectory, "not-existing"),
            IncludePatterns = "*.txt",
            ExcludePatterns = "*.exclude.*",
            Recursive = true
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, _manifestLoader, options, _fileSystem);

        // Act
        var searchMethod = container.GetType().GetMethod("GetPluginAssemblyPaths", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var result = (searchMethod.Invoke(container, null) as List<string>)!;

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetPluginManifests_ReturnsNoResult_WhenNoAssembliesFound()
    {
        // Arrange
        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = Path.Combine(AppContext.BaseDirectory, "not-existing"),
            IncludePatterns = "*.txt",
            ExcludePatterns = "*.exclude.*",
            Recursive = true
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, _manifestLoader, options, _fileSystem);

        // Act
        var result = container.GetPluginManifests();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetPluginManifests_ReturnsNoResult_WhenNoAssembliesWithManifestFound()
    {
        // Arrange
        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = AppContext.BaseDirectory,
            IncludePatterns = "TL.OpcUa.Server.Utils.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, _manifestLoader, options, _fileSystem);

        // Act
        var result = container.GetPluginManifests().ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetPluginManifests_ReturnsManifest_ForAssemblyLoadedInDefaultContext()
    {
        // Arrange
        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = AppContext.BaseDirectory,
            IncludePatterns = "TL.Plugin.OpcUa.NodeSetImporter.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, _manifestLoader, options, _fileSystem);

        // Act
        var result = container.GetPluginManifests().ToList();

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void GetPluginManifests_ReturnsManifest_ForAssemblyLoadedInPluginAssemblyLoadContext()
    {
        var destinationPlugin = Path.Combine(AppContext.BaseDirectory, "test-plugins", "TL.Plugin.OpcUa.NodeSetImporter.dll");
        _fileSystem.File.Copy(Path.Combine(AppContext.BaseDirectory, "TL.Plugin.OpcUa.NodeSetImporter.dll"), destinationPlugin, true);

        // Arrange
        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = Path.Combine(AppContext.BaseDirectory, "test-plugins"),
            IncludePatterns = "TL.Plugin.OpcUa.NodeSetImporter.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, _manifestLoader, options, _fileSystem);

        // Act
        var result = container.GetPluginManifests().ToList();

        // Assert
        Assert.Single(result);
    }
}