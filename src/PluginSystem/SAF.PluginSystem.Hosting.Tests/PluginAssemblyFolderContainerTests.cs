// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Contracts;
using SAF.PluginSystem.Hosting.AssemblyLoading;
using SAF.PluginSystem.Hosting.Tests.AssemblyLoading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SAF.PluginSystem.Hosting.Extensions;
using System.Reflection;
using Testably.Abstractions;

[Collection("BaseDirectoryFileSystem")]
public class PluginAssemblyFolderContainerTests
{
    private readonly RealFileSystem _fileSystem;
    private readonly NullLoggerFactory _loggerFactory;
    private readonly PluginManifestLoader _manifestLoader;

    public PluginAssemblyFolderContainerTests()
    {
        // The tests enumerate real plugin assemblies on disk and load them via reflection /
        // AssemblyLoadContext, which always read from the real file system, so a mock cannot be used.
        _fileSystem = new RealFileSystem();
        _loggerFactory = NullLoggerFactory.Instance;
        _manifestLoader = new PluginManifestLoader();
    }

    [Fact]
    public void GetPluginManifests_ConsidersSubdirectories_WhenRecursiveIsEnabled()
    {
        // Arrange
        var testDirectory = Path.Combine(AppContext.BaseDirectory, $"test-plugins-{Guid.NewGuid():N}");
        _fileSystem.Directory.CreateDirectory(testDirectory);
        var subDirectory = Path.Combine(testDirectory, "sub");
        _fileSystem.Directory.CreateDirectory(subDirectory);

        _fileSystem.File.Copy(Path.Combine(AppContext.BaseDirectory, "SAF.PluginSystem.Hosting.Tests.dll"), Path.Combine(testDirectory, "root.include.dll"));
        _fileSystem.File.Copy(Path.Combine(AppContext.BaseDirectory, "SAF.PluginSystem.Hosting.Tests.dll"), Path.Combine(subDirectory, "subdir.include.dll"));
        _fileSystem.File.Copy(Path.Combine(AppContext.BaseDirectory, "SAF.PluginSystem.Hosting.Tests.dll"), Path.Combine(testDirectory, "root.exclude.dll"));

        var manifestLoader = Substitute.For<IPluginManifestLoader>();
        manifestLoader.LoadPluginManifest(Arg.Any<Assembly>()).Returns(Substitute.For<IPluginManifest>());

        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = testDirectory,
            IncludePatterns = "*.dll",
            ExcludePatterns = "*.exclude.*",
            Recursive = true
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, [], _fileSystem, TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        // Act
        var result = container.GetPluginManifests().ToList();

        // Assert
        Assert.Equal(2, result.Count);
        manifestLoader.Received(2).LoadPluginManifest(Arg.Any<Assembly>());
    }

    [Fact]
    public void GetPluginManifests_DoesNotConsiderSubdirectories_WhenRecursiveIsDisabled()
    {
        // Arrange
        var testDirectory = Path.Combine(AppContext.BaseDirectory, $"test-plugins-{Guid.NewGuid():N}");
        _fileSystem.Directory.CreateDirectory(testDirectory);
        var subDirectory = Path.Combine(testDirectory, "sub");
        _fileSystem.Directory.CreateDirectory(subDirectory);

        _fileSystem.File.Copy(Path.Combine(AppContext.BaseDirectory, "SAF.PluginSystem.Hosting.Tests.dll"), Path.Combine(testDirectory, "root.include.dll"));
        _fileSystem.File.Copy(Path.Combine(AppContext.BaseDirectory, "SAF.PluginSystem.Hosting.Tests.dll"), Path.Combine(subDirectory, "subdir.include.dll"));

        var manifestLoader = Substitute.For<IPluginManifestLoader>();
        manifestLoader.LoadPluginManifest(Arg.Any<Assembly>()).Returns(Substitute.For<IPluginManifest>());

        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = testDirectory,
            IncludePatterns = "*.dll",
            ExcludePatterns = "*.exclude.*",
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, [], _fileSystem, TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        // Act
        var result = container.GetPluginManifests().ToList();

        // Assert
        Assert.Single(result);
        manifestLoader.Received(1).LoadPluginManifest(Arg.Any<Assembly>());
    }

    [Fact]
    public void GetPluginManifests_ReturnsNoResult_WhenSearchPathNotFound()
    {
        // Arrange
        var manifestLoader = Substitute.For<IPluginManifestLoader>();

        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = Path.Combine(AppContext.BaseDirectory, "not-existing"),
            IncludePatterns = "*.dll",
            ExcludePatterns = "*.exclude.*",
            Recursive = true
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, [], _fileSystem, TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        // Act
        var result = container.GetPluginManifests().ToList();

        // Assert
        Assert.Empty(result);
        manifestLoader.DidNotReceive().LoadPluginManifest(Arg.Any<Assembly>());
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
        var container = new PluginAssemblyFolderContainer(_loggerFactory, _manifestLoader, options, [], _fileSystem, TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

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
        var container = new PluginAssemblyFolderContainer(_loggerFactory, _manifestLoader, options, [], _fileSystem, TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

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
            IncludePatterns = "SAF.PluginSystem.Hosting.Tests.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, _manifestLoader, options, [], _fileSystem, TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        // Act
        var result = container.GetPluginManifests().ToList();

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void GetPluginManifests_ReturnsManifest_ForAssemblyLoadedInPluginAssemblyLoadContext()
    {
        var pluginOutputDirectory = Path.Combine(AppContext.BaseDirectory, "plugins", "TestPlugin.PluginA");
        var destinationPluginDirectory = Path.Combine(AppContext.BaseDirectory, "test-plugins");
        var destinationPlugin = Path.Combine(destinationPluginDirectory, "TestPlugin.PluginA.dll");
        var destinationPluginDeps = Path.Combine(destinationPluginDirectory, "TestPlugin.PluginA.deps.json");
        var destinationDependency = Path.Combine(destinationPluginDirectory, "TestPlugin.DependencyA.dll");

        if (!_fileSystem.File.Exists(destinationPlugin))
        {
            _fileSystem.File.Copy(Path.Combine(pluginOutputDirectory, "TestPlugin.PluginA.dll"), destinationPlugin);
        }

        if (!_fileSystem.File.Exists(destinationPluginDeps))
        {
            _fileSystem.File.Copy(Path.Combine(pluginOutputDirectory, "TestPlugin.PluginA.deps.json"), destinationPluginDeps);
        }

        if (!_fileSystem.File.Exists(destinationDependency))
        {
            _fileSystem.File.Copy(Path.Combine(pluginOutputDirectory, "TestPlugin.DependencyA.dll"), destinationDependency);
        }

        // Arrange
        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = Path.Combine(AppContext.BaseDirectory, "test-plugins"),
            IncludePatterns = "TestPlugin.PluginA.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, _manifestLoader, options, [], _fileSystem, TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        // Act
        var result = container.GetPluginManifests().ToList();

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void GetPluginManifests_ReturnsSameInstance_OnSecondCall()
    {
        // Arrange
        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = AppContext.BaseDirectory,
            IncludePatterns = "SAF.PluginSystem.Hosting.Tests.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, _manifestLoader, options, [], _fileSystem, TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        // Act
        var firstCall = container.GetPluginManifests();
        var secondCall = container.GetPluginManifests();

        // Assert – must be the exact same list instance, not just equal content
        Assert.Same(firstCall, secondCall);
    }

    [Fact]
    public void GetPluginManifests_LoadsAssembliesOnlyOnce()
    {
        // Arrange
        var manifestLoader = Substitute.For<IPluginManifestLoader>();
        manifestLoader.LoadPluginManifest(Arg.Any<Assembly>())
            .Returns(Substitute.For<IPluginManifest>());

        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = AppContext.BaseDirectory,
            IncludePatterns = "SAF.PluginSystem.Hosting.Tests.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, [], _fileSystem, TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        // Act – call twice
        _ = container.GetPluginManifests().ToList();
        _ = container.GetPluginManifests().ToList();

        // Assert – manifest loader is called exactly once per assembly, not twice
        manifestLoader.Received(1).LoadPluginManifest(Arg.Any<Assembly>());
    }

    [Fact]
    public void GetPluginManifests_ReturnsSameManifestInstances_OnSecondCall()
    {
        // Arrange
        var manifest = Substitute.For<IPluginManifest>();
        var manifestLoader = Substitute.For<IPluginManifestLoader>();
        manifestLoader.LoadPluginManifest(Arg.Any<Assembly>()).Returns(manifest);

        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = AppContext.BaseDirectory,
            IncludePatterns = "SAF.PluginSystem.Hosting.Tests.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, [], _fileSystem, TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        // Act
        var firstCall = container.GetPluginManifests().ToList();
        var secondCall = container.GetPluginManifests().ToList();

        // Assert – same manifest objects returned, not new ones
        Assert.Equal(firstCall, secondCall);
    }

    [Fact]
    public void GetPluginManifests_ContinuesWhenAssemblyCannotBeLoaded()
    {
        // Arrange
        var testDirectory = Path.Combine(AppContext.BaseDirectory, $"test-plugins-{Guid.NewGuid():N}");
        _fileSystem.Directory.CreateDirectory(testDirectory);

        var invalidAssemblyPath = Path.Combine(testDirectory, "invalid.native.dll");
        _fileSystem.File.WriteAllBytes(invalidAssemblyPath, [0x01, 0x02, 0x03, 0x04]);

        var validAssemblyPath = Path.Combine(testDirectory, "valid.managed.dll");
        _fileSystem.File.Copy(Path.Combine(AppContext.BaseDirectory, "SAF.PluginSystem.Hosting.Tests.dll"), validAssemblyPath);

        var manifest = Substitute.For<IPluginManifest>();
        var manifestLoader = Substitute.For<IPluginManifestLoader>();
        manifestLoader.LoadPluginManifest(Arg.Any<Assembly>()).Returns(manifest);

        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = testDirectory,
            IncludePatterns = "*.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };

        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, [], _fileSystem, TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        // Act
        var result = container.GetPluginManifests().ToList();

        // Assert
        Assert.Single(result);
        Assert.Same(manifest, result[0]);
        manifestLoader.Received(1).LoadPluginManifest(Arg.Any<Assembly>());
    }

    [Fact]
    public void GetPluginManifests_SkipsAssembly_WhenPublicKeyTokenIsNotAllowed()
    {
        var manifestLoader = Substitute.For<IPluginManifestLoader>();
        manifestLoader.LoadPluginManifest(Arg.Any<Assembly>()).Returns(Substitute.For<IPluginManifest>());

        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = AppContext.BaseDirectory,
            IncludePatterns = "SAF.PluginSystem.Hosting.Tests.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var validatorOptions = new StrongNamePluginAssemblyValidatorOptions();
        validatorOptions.AllowedPublicKeyTokens.Add("0011223344556677");

        var container = new PluginAssemblyFolderContainer(
            _loggerFactory,
            manifestLoader,
            options,
            [new StrongNamePluginAssemblyValidator(Options.Create(validatorOptions))],
            _fileSystem,
            TestSharedAssemblyResolver.SharesHostProvidedAssemblies,
            SharedAssemblyConflictBehavior.Fail);

        var result = container.GetPluginManifests().ToList();

        Assert.Empty(result);
        manifestLoader.DidNotReceive().LoadPluginManifest(Arg.Any<Assembly>());
    }

    [Fact]
    public void GetPluginManifests_SkipsAssembly_WhenCustomValidationRejects()
    {
        var manifestLoader = Substitute.For<IPluginManifestLoader>();
        manifestLoader.LoadPluginManifest(Arg.Any<Assembly>()).Returns(Substitute.For<IPluginManifest>());

        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = AppContext.BaseDirectory,
            IncludePatterns = "SAF.PluginSystem.Hosting.Tests.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, [new RejectingPluginAssemblyValidator()], _fileSystem, TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        var result = container.GetPluginManifests().ToList();

        Assert.Empty(result);
        manifestLoader.DidNotReceive().LoadPluginManifest(Arg.Any<Assembly>());
    }

    [Fact]
    public void GetPluginManifests_Rethrows_WhenSharedAssemblyVersionConflictWrappedInFileLoadException()
    {
        var testDirectory = Path.Combine(AppContext.BaseDirectory, $"test-plugins-{Guid.NewGuid():N}");
        _fileSystem.Directory.CreateDirectory(testDirectory);

        var pluginAssemblyPath = Path.Combine(testDirectory, "valid.managed.dll");
        _fileSystem.File.Copy(Path.Combine(AppContext.BaseDirectory, "SAF.PluginSystem.Hosting.Tests.dll"), pluginAssemblyPath);

        var conflict = new SharedAssemblyVersionConflictException("Acme.Contracts", new Version(2, 0, 0, 0), new Version(1, 0, 0, 0));
        var manifestLoader = Substitute.For<IPluginManifestLoader>();
        manifestLoader.When(x => x.LoadPluginManifest(Arg.Any<Assembly>()))
            .Do(_ => throw new FileLoadException("wrapped by runtime", conflict));

        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = testDirectory,
            IncludePatterns = "*.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, [], _fileSystem, TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        var thrown = Assert.Throws<FileLoadException>(() => container.GetPluginManifests().ToList());
        Assert.Same(conflict, thrown.InnerException);
    }

    private sealed class RejectingPluginAssemblyValidator : IPluginAssemblyValidator
    {
        public PluginAssemblyValidationResult Validate(PluginAssemblyValidationContext context)
            => PluginAssemblyValidationResult.Rejected("Rejected by test");
    }
}