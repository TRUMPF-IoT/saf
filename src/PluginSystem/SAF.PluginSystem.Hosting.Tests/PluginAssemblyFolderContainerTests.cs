// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SAF.PluginSystem.Hosting.Extensions;
using System.Reflection;
using Testably.Abstractions;

public sealed class PluginAssemblyFolderContainerTests : IDisposable
{
    private readonly RealFileSystem _fileSystem;
    private readonly string _testRootPath;
    private readonly string _testPluginsPath;
    private readonly string _testAssemblyPath;
    private readonly NullLoggerFactory _loggerFactory;
    private readonly PluginManifestLoader _manifestLoader;

    public PluginAssemblyFolderContainerTests()
    {
        // The tests enumerate real plugin assemblies on disk and load them via reflection /
        // AssemblyLoadContext, which always read from the real file system, so a mock cannot be used.
        _fileSystem = new RealFileSystem();
        _testRootPath = Path.Combine(Path.GetTempPath(), $"saf-plugin-folder-container-tests-{Guid.NewGuid():N}");
        _testPluginsPath = Path.Combine(_testRootPath, "test-plugins");
        _testAssemblyPath = Path.Combine(_testRootPath, "SAF.PluginSystem.Hosting.Tests.dll");
        _fileSystem.Directory.CreateDirectory(_testRootPath);
        _fileSystem.Directory.CreateDirectory(_testPluginsPath);
        _fileSystem.File.Copy(Path.Combine(AppContext.BaseDirectory, "SAF.PluginSystem.Hosting.Tests.dll"), _testAssemblyPath, true);

        _loggerFactory = NullLoggerFactory.Instance;
        _manifestLoader = new PluginManifestLoader();
    }

    [Fact]
    public void GetPluginManifests_ConsidersSubdirectories_WhenRecursiveIsEnabled()
    {
        // Arrange
        var testDirectory = CreateTestDirectory($"test-plugins-{Guid.NewGuid():N}");
        var subDirectory = Path.Combine(testDirectory, "sub");
        _fileSystem.Directory.CreateDirectory(subDirectory);

        _fileSystem.File.Copy(_testAssemblyPath, Path.Combine(testDirectory, "root.include.dll"));
        _fileSystem.File.Copy(_testAssemblyPath, Path.Combine(subDirectory, "subdir.include.dll"));
        _fileSystem.File.Copy(_testAssemblyPath, Path.Combine(testDirectory, "root.exclude.dll"));

        var manifestLoader = Substitute.For<IPluginManifestLoader>();
        manifestLoader.LoadPluginManifest(Arg.Any<Assembly>()).Returns(Substitute.For<IPluginManifest>());

        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = testDirectory,
            IncludePatterns = "*.dll",
            ExcludePatterns = "*.exclude.*",
            Recursive = true
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, [], _fileSystem);

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
        var testDirectory = CreateTestDirectory($"test-plugins-{Guid.NewGuid():N}");
        var subDirectory = Path.Combine(testDirectory, "sub");
        _fileSystem.Directory.CreateDirectory(subDirectory);

        _fileSystem.File.Copy(_testAssemblyPath, Path.Combine(testDirectory, "root.include.dll"));
        _fileSystem.File.Copy(_testAssemblyPath, Path.Combine(subDirectory, "subdir.include.dll"));

        var manifestLoader = Substitute.For<IPluginManifestLoader>();
        manifestLoader.LoadPluginManifest(Arg.Any<Assembly>()).Returns(Substitute.For<IPluginManifest>());

        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = testDirectory,
            IncludePatterns = "*.dll",
            ExcludePatterns = "*.exclude.*",
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, [], _fileSystem);

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
            SearchRootPath = Path.Combine(_testRootPath, "not-existing"),
            IncludePatterns = "*.dll",
            ExcludePatterns = "*.exclude.*",
            Recursive = true
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, [], _fileSystem);

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
            SearchRootPath = Path.Combine(_testRootPath, "not-existing"),
            IncludePatterns = "*.txt",
            ExcludePatterns = "*.exclude.*",
            Recursive = true
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, _manifestLoader, options, [], _fileSystem);

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
            SearchRootPath = _testRootPath,
            IncludePatterns = "TL.OpcUa.Server.Utils.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, _manifestLoader, options, [], _fileSystem);

        // Act
        var result = container.GetPluginManifests().ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetPluginManifests_ReturnsManifest_ForAssemblyLoadedInDefaultContext()
    {
        // Arrange
        var manifestLoader = Substitute.For<IPluginManifestLoader>();
        manifestLoader.LoadPluginManifest(Arg.Any<Assembly>()).Returns(Substitute.For<IPluginManifest>());

        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = AppContext.BaseDirectory,
            IncludePatterns = "SAF.PluginSystem.Hosting.Tests.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, [], _fileSystem);

        // Act
        var result = container.GetPluginManifests().ToList();

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void GetPluginManifests_ReturnsManifest_ForAssemblyLoadedInPluginAssemblyLoadContext()
    {
        var pluginOutputDirectory = Path.Combine(AppContext.BaseDirectory, "plugins", "TestPlugin.PluginA");
        var destinationPlugin = Path.Combine(_testPluginsPath, "TestPlugin.PluginA.dll");
        var destinationPluginDeps = Path.Combine(_testPluginsPath, "TestPlugin.PluginA.deps.json");
        var destinationDependency = Path.Combine(_testPluginsPath, "TestPlugin.DependencyA.dll");

        _fileSystem.File.Copy(Path.Combine(pluginOutputDirectory, "TestPlugin.PluginA.dll"), destinationPlugin, true);
        _fileSystem.File.Copy(Path.Combine(pluginOutputDirectory, "TestPlugin.PluginA.deps.json"), destinationPluginDeps, true);
        _fileSystem.File.Copy(Path.Combine(pluginOutputDirectory, "TestPlugin.DependencyA.dll"), destinationDependency, true);

        // Arrange
        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = _testPluginsPath,
            IncludePatterns = "TestPlugin.PluginA.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, _manifestLoader, options, [], _fileSystem);

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
            SearchRootPath = _testRootPath,
            IncludePatterns = Path.GetFileName(_testAssemblyPath),
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, _manifestLoader, options, [], _fileSystem);

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
            SearchRootPath = _testRootPath,
            IncludePatterns = Path.GetFileName(_testAssemblyPath),
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, [], _fileSystem);

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
            SearchRootPath = _testRootPath,
            IncludePatterns = Path.GetFileName(_testAssemblyPath),
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, [], _fileSystem);

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
        var testDirectory = CreateTestDirectory($"test-plugins-{Guid.NewGuid():N}");

        var invalidAssemblyPath = Path.Combine(testDirectory, "invalid.native.dll");
        _fileSystem.File.WriteAllBytes(invalidAssemblyPath, [0x01, 0x02, 0x03, 0x04]);

        var validAssemblyPath = Path.Combine(testDirectory, "valid.managed.dll");
        _fileSystem.File.Copy(_testAssemblyPath, validAssemblyPath);

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

        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, [], _fileSystem);

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
        var optionsMonitor = Substitute.For<IOptionsMonitor<StrongNamePluginAssemblyValidatorOptions>>();
        optionsMonitor.Get(Options.DefaultName).Returns(validatorOptions);

        var container = new PluginAssemblyFolderContainer(
            _loggerFactory,
            manifestLoader,
            options,
            [new StrongNamePluginAssemblyValidator(optionsMonitor)],
            _fileSystem);

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
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, [new RejectingPluginAssemblyValidator()], _fileSystem);

        var result = container.GetPluginManifests().ToList();

        Assert.Empty(result);
        manifestLoader.DidNotReceive().LoadPluginManifest(Arg.Any<Assembly>());
    }

    [Fact]
    public void GetPluginManifests_HoldsReadShareLock_ThroughAssemblyLoad()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "File share locking is platform-specific.");

        var manifestLoader = new WriteCheckingManifestLoader(_testAssemblyPath);
        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = _testRootPath,
            IncludePatterns = Path.GetFileName(_testAssemblyPath),
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, [], _fileSystem);

        var result = container.GetPluginManifests().ToList();

        Assert.Single(result);
        Assert.True(manifestLoader.WriteWasBlocked);
    }

    [Fact]
    public void GetPluginManifests_LoadsSnapshot_WhenAssemblyPathChangesAfterValidation()
    {
        var manifestLoader = Substitute.For<IPluginManifestLoader>();
        manifestLoader.LoadPluginManifest(Arg.Any<Assembly>()).Returns(Substitute.For<IPluginManifest>());

        var validator = new ReplacingPluginAssemblyValidator(_testAssemblyPath);
        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = _testRootPath,
            IncludePatterns = Path.GetFileName(_testAssemblyPath),
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, [validator], _fileSystem);

        var result = container.GetPluginManifests().ToList();

        Assert.Single(result);
        Assert.True(validator.ReceivedContentSnapshot);
    }

    private sealed class RejectingPluginAssemblyValidator : IPluginAssemblyValidator
    {
        public PluginAssemblyValidationResult Validate(PluginAssemblyValidationContext context)
            => PluginAssemblyValidationResult.Rejected("Rejected by test");
    }

    private sealed class WriteCheckingManifestLoader(string assemblyPath) : IPluginManifestLoader
    {
        public bool WriteWasBlocked { get; private set; }

        public IPluginManifest? LoadPluginManifest(Assembly assembly)
        {
            try
            {
                using var writeStream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Write, FileShare.Read);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                WriteWasBlocked = true;
            }

            return Substitute.For<IPluginManifest>();
        }
    }

    private sealed class ReplacingPluginAssemblyValidator(string assemblyPath) : IPluginAssemblyValidator
    {
        public bool ReceivedContentSnapshot { get; private set; }

        public PluginAssemblyValidationResult Validate(PluginAssemblyValidationContext context)
        {
            ReceivedContentSnapshot = !context.AssemblyBytes.IsEmpty;

            try
            {
                File.WriteAllBytes(assemblyPath, [0x01, 0x02, 0x03, 0x04]);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

            return PluginAssemblyValidationResult.Accepted();
        }
    }

    public void Dispose()
    {
        if (!_fileSystem.Directory.Exists(_testRootPath))
        {
            return;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                _fileSystem.Directory.Delete(_testRootPath, true);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                // intentionally left empty to retry
            } catch (UnauthorizedAccessException) when (attempt < 4)
            {
                // intentionally left empty to retry
            } catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }

    private string CreateTestDirectory(string name)
    {
        var directory = Path.Combine(_testRootPath, name);
        _fileSystem.Directory.CreateDirectory(directory);
        return directory;
    }
}
