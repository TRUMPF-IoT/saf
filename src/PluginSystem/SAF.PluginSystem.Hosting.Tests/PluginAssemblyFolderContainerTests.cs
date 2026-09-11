// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Contracts;
using Microsoft.Extensions.Logging;
using SAF.PluginSystem.Hosting.AssemblyLoading;
using SAF.PluginSystem.Hosting.Tests.AssemblyLoading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SAF.PluginSystem.Hosting.Extensions;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.Runtime.Versioning;
using System.IO.Abstractions;
using System.Security.AccessControl;
using System.Security.Principal;
using Testably.Abstractions;

[Collection("BaseDirectoryFileSystem")]
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
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, _fileSystem, [], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

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
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, _fileSystem, [], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

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
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, _fileSystem, [], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

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
        var container = new PluginAssemblyFolderContainer(_loggerFactory, _manifestLoader, options, _fileSystem, [], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

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
        var container = new PluginAssemblyFolderContainer(_loggerFactory, _manifestLoader, options, _fileSystem, [], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        // Act
        var result = container.GetPluginManifests().ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetPluginManifests_ReturnsManifest_ForAssemblyLoadedInDefaultContext()
    {
        // Arrange
        Assembly? loadedAssembly = null;
        var manifestLoader = Substitute.For<IPluginManifestLoader>();
        manifestLoader.LoadPluginManifest(Arg.Do<Assembly>(a => loadedAssembly = a)).Returns(Substitute.For<IPluginManifest>());

        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = AppContext.BaseDirectory,
            IncludePatterns = "SAF.PluginSystem.Hosting.Tests.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, _fileSystem, [], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        // Act
        var result = container.GetPluginManifests().ToList();

        // Assert
        Assert.Single(result);

        // A base-directory candidate is loaded via AssemblyLoadContext.Default, not a PluginAssemblyLoadContext,
        // so neither isolation nor shared-assembly conflict detection ever applies to it (documented in
        // docs/plugin-system.md).
        Assert.Same(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(loadedAssembly!));
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
        var container = new PluginAssemblyFolderContainer(_loggerFactory, _manifestLoader, options, _fileSystem, [], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

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
        var container = new PluginAssemblyFolderContainer(_loggerFactory, _manifestLoader, options, _fileSystem, [], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

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
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, _fileSystem, [], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

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
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, _fileSystem, [], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

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

        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, _fileSystem, [], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        // Act
        var result = container.GetPluginManifests().ToList();

        // Assert
        Assert.Single(result);
        Assert.Same(manifest, result[0]);
        manifestLoader.Received(1).LoadPluginManifest(Arg.Any<Assembly>());
    }

    [Fact]
    public void GetPluginManifests_DoesNotReadFileContent_WhenNoValidatorsAreConfigured()
    {
        // Arrange
        var testDirectory = CreateTestDirectory($"test-plugins-{Guid.NewGuid():N}");
        var pluginPath = Path.Combine(testDirectory, "valid.managed.dll");
        _fileSystem.File.Copy(_testAssemblyPath, pluginPath);

        CountingStream? countingStream = null;
        var fileInfo = Substitute.For<IFileInfo>();
        fileInfo.Open(Arg.Any<FileMode>(), Arg.Any<FileAccess>(), Arg.Any<FileShare>())
            .Returns(_ => countingStream = new CountingStream(pluginPath));

        var fileInfoFactory = Substitute.For<IFileInfoFactory>();
        fileInfoFactory.New(Arg.Any<string>()).Returns(fileInfo);

        // Only FileInfo is faked; Directory and Path pass straight through to the real file system so
        // directory enumeration and path resolution behave normally.
        var fileSystem = Substitute.For<IFileSystem>();
        fileSystem.Directory.Returns(_fileSystem.Directory);
        fileSystem.Path.Returns(_fileSystem.Path);
        fileSystem.FileInfo.Returns(fileInfoFactory);

        var manifestLoader = Substitute.For<IPluginManifestLoader>();
        manifestLoader.LoadPluginManifest(Arg.Any<Assembly>()).Returns(Substitute.For<IPluginManifest>());

        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = testDirectory,
            IncludePatterns = "*.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(
            _loggerFactory, manifestLoader, options, fileSystem, [], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        // Act
        var result = container.GetPluginManifests().ToList();

        // Assert - the file is opened (to pin it and hand a stream to the loader), but with no
        // validators configured, its content is never read into memory.
        Assert.Single(result);
        Assert.NotNull(countingStream);
        Assert.Equal(0, countingStream.ReadCount);
    }

    [Fact]
    public void GetPluginManifests_LogsWarning_NotError_WhenNativeOrCorruptDllMatchesSearchPattern_AndNoValidatorsAreConfigured()
    {
        // Arrange - without validators, GetAssemblyName no longer pre-filters candidates (2.8), so a
        // native/corrupt DLL now reaches LoadFromAssemblyPath instead of being rejected during validation.
        var testDirectory = CreateTestDirectory($"test-plugins-{Guid.NewGuid():N}");
        var invalidAssemblyPath = Path.Combine(testDirectory, "invalid.native.dll");
        _fileSystem.File.WriteAllBytes(invalidAssemblyPath, [0x01, 0x02, 0x03, 0x04]);

        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = testDirectory,
            IncludePatterns = "*.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var loggerFactory = new CapturingLoggerFactory();
        var container = new PluginAssemblyFolderContainer(
            loggerFactory, _manifestLoader, options, _fileSystem, [], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        // Act
        var result = container.GetPluginManifests().ToList();

        // Assert - skipped, not a load failure: the old severity for an ordinary non-plugin DLL.
        Assert.Empty(result);
        Assert.Contains(loggerFactory.Entries, e =>
            e.Level == LogLevel.Warning && e.Message.Contains("Failed to load plugin manifest", StringComparison.Ordinal));
        Assert.DoesNotContain(loggerFactory.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public void GetPluginManifests_SkipsAssembly_WhenCultureMetadataIsMalformed()
    {
        // Arrange - a candidate whose raw assembly metadata carries a culture string that
        // AssemblyName.CultureName's setter rejects with CultureNotFoundException (an ArgumentException).
        // This bypasses the managed AssemblyName validation entirely, the way a hand-crafted or
        // corrupted satellite resource assembly would.
        var testDirectory = CreateTestDirectory($"test-plugins-{Guid.NewGuid():N}");

        var malformedCultureAssemblyPath = Path.Combine(testDirectory, "malformed.culture.dll");
        _fileSystem.File.WriteAllBytes(
            malformedCultureAssemblyPath,
            BuildAssemblyWithCulture("MalformedCulture", "not-a-real-culture!!!"));

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
        var loggerFactory = new CapturingLoggerFactory();
        var container = new PluginAssemblyFolderContainer(
            loggerFactory, manifestLoader, options, _fileSystem, [new AcceptingPluginAssemblyValidator()], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        // Act
        var result = container.GetPluginManifests().ToList();

        // Assert - the malformed candidate is skipped, not thrown, and the other candidate still loads
        Assert.Single(result);
        Assert.Same(manifest, result[0]);
        Assert.Contains(loggerFactory.Entries, entry =>
            entry.Level == LogLevel.Warning && entry.Message.Contains("metadata could not be read", StringComparison.Ordinal));
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
            _fileSystem,
            [new StrongNamePluginAssemblyValidator(optionsMonitor)], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

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
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, _fileSystem, [new RejectingPluginAssemblyValidator()], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

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
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, _fileSystem, [], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        var result = container.GetPluginManifests().ToList();

        Assert.Single(result);
        Assert.True(manifestLoader.WriteWasBlocked);
    }

    [Fact]
    public void GetPluginManifests_NeverLoadsContent_ThatWasNotValidated()
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
        var loggerFactory = new CapturingLoggerFactory();
        var container = new PluginAssemblyFolderContainer(loggerFactory, manifestLoader, options, _fileSystem, [validator], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        var result = container.GetPluginManifests().ToList();

        Assert.True(validator.ReceivedContentSnapshot);

        if (OperatingSystem.IsWindows())
        {
            // The pinning handle denies the write, so the validated file is still the one that is loaded.
            Assert.Single(result);
        }
        else
        {
            // The replacement succeeds, and the check before the load must catch it.
            Assert.Empty(result);
            Assert.Contains(loggerFactory.Entries, entry => entry.Message.Contains("changed after it was validated", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void MatchesFileContent_ReturnsTrue_ForUnchangedFile()
    {
        var path = Path.Combine(_testRootPath, $"compare-{Guid.NewGuid():N}.bin");
        var content = new byte[(64 * 1024) + 137];
        Random.Shared.NextBytes(content);
        _fileSystem.File.WriteAllBytes(path, content);

        Assert.True(PluginAssemblyFolderContainer.MatchesFileContent(_fileSystem, path, content));
    }

    [Fact]
    public void MatchesFileContent_ReturnsFalse_WhenContentOrLengthChanged()
    {
        var path = Path.Combine(_testRootPath, $"compare-{Guid.NewGuid():N}.bin");
        var content = new byte[(64 * 1024) + 137];
        Random.Shared.NextBytes(content);

        // Same length, different content in the second buffer window.
        _fileSystem.File.WriteAllBytes(path, content);
        var modified = (byte[])content.Clone();
        modified[^1] ^= 0xFF;
        Assert.False(PluginAssemblyFolderContainer.MatchesFileContent(_fileSystem, path, modified));

        // Shorter and longer than the validated content.
        _fileSystem.File.WriteAllBytes(path, content[..1024]);
        Assert.False(PluginAssemblyFolderContainer.MatchesFileContent(_fileSystem, path, content));
        _fileSystem.File.WriteAllBytes(path, [.. content, 0x00]);
        Assert.False(PluginAssemblyFolderContainer.MatchesFileContent(_fileSystem, path, content));
    }

    [Fact]
    public void ReadAllBytes_ReadsTheWholeFile()
    {
        var content = new byte[(128 * 1024) + 17];
        Random.Shared.NextBytes(content);
        var path = Path.Combine(_testRootPath, $"read-{Guid.NewGuid():N}.bin");
        _fileSystem.File.WriteAllBytes(path, content);

        using var stream = _fileSystem.FileInfo.New(path).Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        var result = PluginAssemblyFolderContainer.ReadAllBytes(stream);

        Assert.Equal(content, result);
    }

    [Fact]
    public void ReadAllBytes_Fails_WhenTheFileExceedsTheLargestBuffer()
    {
        using var stream = new OversizedStream();

        // Truncating the length to int would validate less content than is later loaded.
        Assert.Throws<IOException>(() => PluginAssemblyFolderContainer.ReadAllBytes(stream));
    }

    [Fact]
    public void GetPluginManifests_PopulatesAssemblyLocation_ForPluginOutsideBaseDirectory()
    {
        var testDirectory = CreateTestDirectory($"test-plugins-{Guid.NewGuid():N}");
        var pluginPath = Path.Combine(testDirectory, "located.include.dll");
        _fileSystem.File.Copy(_testAssemblyPath, pluginPath);

        var manifestLoader = new LocationCapturingManifestLoader();
        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = testDirectory,
            IncludePatterns = "*.include.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(
            _loggerFactory, manifestLoader, options, _fileSystem, [new AcceptingPluginAssemblyValidator()], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        var result = container.GetPluginManifests().ToList();

        Assert.Single(result);

        // The deployment path itself, so that code resolving resources next to Assembly.Location works.
        Assert.Equal(pluginPath, manifestLoader.CapturedLocation, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetPluginManifests_ResolvesPluginDependencies_FromDeploymentFolder()
    {
        var pluginOutputDirectory = Path.Combine(AppContext.BaseDirectory, "plugins", "TestPlugin.PluginA");
        var pluginDirectory = CreateTestDirectory($"test-plugins-{Guid.NewGuid():N}");
        foreach (var filePath in _fileSystem.Directory.GetFiles(pluginOutputDirectory))
        {
            _fileSystem.File.Copy(filePath, Path.Combine(pluginDirectory, Path.GetFileName(filePath)));
        }

        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = pluginDirectory,
            IncludePatterns = "TestPlugin.PluginA.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, _manifestLoader, options, _fileSystem, [], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        var manifest = Assert.Single(container.GetPluginManifests());

        var entryType = manifest.GetType().Assembly.GetType("TestPlugin.PluginA.PluginAEntry");
        Assert.NotNull(entryType);
        var dependency = (Assembly)entryType.GetMethod("GetDependencyAssembly")!.Invoke(null, null)!;

        // The dependency must resolve through the .deps.json of the deployment folder.
        Assert.Equal("TestPlugin.DependencyA", dependency.GetName().Name);
        Assert.Equal(pluginDirectory, Path.GetDirectoryName(dependency.Location), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetPluginManifests_SkipsAssembly_WhenFileIsExclusivelyLocked()
    {
        var testDirectory = CreateTestDirectory($"test-plugins-{Guid.NewGuid():N}");
        var lockedPath = Path.Combine(testDirectory, "locked.include.dll");
        var loadablePath = Path.Combine(testDirectory, "loadable.include.dll");
        _fileSystem.File.Copy(_testAssemblyPath, lockedPath);
        _fileSystem.File.Copy(_testAssemblyPath, loadablePath);

        var manifestLoader = Substitute.For<IPluginManifestLoader>();
        manifestLoader.LoadPluginManifest(Arg.Any<Assembly>()).Returns(Substitute.For<IPluginManifest>());

        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = testDirectory,
            IncludePatterns = "*.include.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, _fileSystem, [], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        using (new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var result = container.GetPluginManifests().ToList();

            Assert.Single(result);
        }
    }

    [Fact]
    public void GetPluginManifests_Throws_WhenPluginTriggersConflictingSharedAssembly_AndBehaviorIsFail()
    {
        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = _testRootPath,
            IncludePatterns = Path.GetFileName(_testAssemblyPath),
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var resolver = new ConflictingSharedAssemblyResolver(new Version(1, 0, 0, 0));
        var container = new PluginAssemblyFolderContainer(
            _loggerFactory, new ConflictTriggeringManifestLoader(), options, _fileSystem, [], resolver, SharedAssemblyConflictBehavior.Fail);

        Assert.Throws<SharedAssemblyVersionConflictException>(() => container.GetPluginManifests().ToList());
    }

    [Fact]
    public void GetPluginManifests_DoesNotThrow_WhenPluginTriggersConflictingSharedAssembly_AndBehaviorIsIsolateWithWarning()
    {
        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = _testRootPath,
            IncludePatterns = Path.GetFileName(_testAssemblyPath),
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var resolver = new ConflictingSharedAssemblyResolver(new Version(1, 0, 0, 0));
        var container = new PluginAssemblyFolderContainer(
            _loggerFactory, new ConflictTriggeringManifestLoader(), options, _fileSystem, [], resolver, SharedAssemblyConflictBehavior.IsolateWithWarning);

        var result = container.GetPluginManifests().ToList();

        Assert.Single(result);
    }

    [Fact]
    public void GetPluginManifests_SkipsAssembly_WhenAccessIsDenied()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Denying read access requires Windows ACLs.");
            return;
        }

        var testDirectory = CreateTestDirectory($"test-plugins-{Guid.NewGuid():N}");
        var deniedPath = Path.Combine(testDirectory, "denied.include.dll");
        var loadablePath = Path.Combine(testDirectory, "loadable.include.dll");
        _fileSystem.File.Copy(_testAssemblyPath, deniedPath);
        _fileSystem.File.Copy(_testAssemblyPath, loadablePath);
        DenyReadAccessForCurrentUser(deniedPath);

        var manifestLoader = Substitute.For<IPluginManifestLoader>();
        manifestLoader.LoadPluginManifest(Arg.Any<Assembly>()).Returns(Substitute.For<IPluginManifest>());

        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = testDirectory,
            IncludePatterns = "*.include.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(_loggerFactory, manifestLoader, options, _fileSystem, [], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        var result = container.GetPluginManifests().ToList();

        Assert.Single(result);
    }

    [Fact]
    public void GetPluginManifests_SkipsAssembly_WhenCustomValidationThrows()
    {
        var testDirectory = CreateTestDirectory($"test-plugins-{Guid.NewGuid():N}");
        var throwingPath = Path.Combine(testDirectory, "throwing.include.dll");
        var loadablePath = Path.Combine(testDirectory, "loadable.include.dll");
        _fileSystem.File.Copy(_testAssemblyPath, throwingPath);
        _fileSystem.File.Copy(_testAssemblyPath, loadablePath);

        var manifestLoader = Substitute.For<IPluginManifestLoader>();
        manifestLoader.LoadPluginManifest(Arg.Any<Assembly>()).Returns(Substitute.For<IPluginManifest>());

        var options = new PluginAssemblyFolderSearchOptions
        {
            SearchRootPath = testDirectory,
            IncludePatterns = "*.include.dll",
            ExcludePatterns = string.Empty,
            Recursive = false
        };
        var container = new PluginAssemblyFolderContainer(
            _loggerFactory, manifestLoader, options, _fileSystem, [new ThrowingPluginAssemblyValidator(throwingPath)], TestSharedAssemblyResolver.SharesHostProvidedAssemblies, SharedAssemblyConflictBehavior.Fail);

        var result = container.GetPluginManifests().ToList();

        Assert.Single(result);
        manifestLoader.Received(1).LoadPluginManifest(Arg.Any<Assembly>());
    }

    [SupportedOSPlatform("windows")]
    private static void DenyReadAccessForCurrentUser(string path)
    {
        var fileInfo = new FileInfo(path);
        var security = fileInfo.GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(
            WindowsIdentity.GetCurrent().User!, FileSystemRights.Read, AccessControlType.Deny));
        fileInfo.SetAccessControl(security);
    }

    private sealed class AcceptingPluginAssemblyValidator : IPluginAssemblyValidator
    {
        public PluginAssemblyValidationResult Validate(PluginAssemblyValidationContext context)
            => PluginAssemblyValidationResult.Accepted();
    }

    /// <summary>
    /// Builds a minimal managed PE image whose assembly-definition row carries the given culture
    /// string verbatim in the metadata string heap - unlike <see cref="AssemblyName.CultureName"/>'s
    /// setter, the raw metadata format does not validate it.
    /// </summary>
    private static byte[] BuildAssemblyWithCulture(string assemblyName, string culture)
    {
        var metadataBuilder = new MetadataBuilder();

        metadataBuilder.AddModule(
            0,
            metadataBuilder.GetOrAddString(assemblyName + ".dll"),
            metadataBuilder.GetOrAddGuid(Guid.NewGuid()),
            default,
            default);

        metadataBuilder.AddAssembly(
            metadataBuilder.GetOrAddString(assemblyName),
            new Version(1, 0, 0, 0),
            metadataBuilder.GetOrAddString(culture),
            default,
            default,
            AssemblyHashAlgorithm.None);

        metadataBuilder.AddTypeDefinition(
            default,
            default,
            metadataBuilder.GetOrAddString("<Module>"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));

        var peBuilder = new ManagedPEBuilder(
            new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll | Characteristics.ExecutableImage),
            new MetadataRootBuilder(metadataBuilder),
            new BlobBuilder());

        var peBlob = new BlobBuilder();
        peBuilder.Serialize(peBlob);
        return peBlob.ToArray();
    }

    /// <summary>
    /// A real file stream (IFileInfo.Open must return a FileSystemStream) that counts calls to
    /// <see cref="Read"/>, which <see cref="Stream.ReadExactly(byte[])"/> loops through internally.
    /// </summary>
    private sealed class CountingStream(string path)
        : FileSystemStream(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), path, isAsync: false)
    {
        public int ReadCount { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadCount++;
            return base.Read(buffer, offset, count);
        }
    }

    private sealed class OversizedStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => (long)Array.MaxLength + 1;

        public override long Position { get; set; }

        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override long Seek(long offset, SeekOrigin origin) => 0;

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush() { }
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        public List<LogEntry> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

        public void AddProvider(ILoggerProvider provider) { }

        public void Dispose() { }

        private sealed class CapturingLogger(List<LogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }

    /// <summary>
    /// Simulates a plugin whose manifest-loading step lazily binds a shared dependency - forcing
    /// PluginAssemblyLoadContext.Load to run for it - without needing a real conflicting assembly on disk.
    /// </summary>
    private sealed class ConflictTriggeringManifestLoader : IPluginManifestLoader
    {
        public IPluginManifest? LoadPluginManifest(Assembly assembly)
        {
            var context = (PluginAssemblyLoadContext)AssemblyLoadContext.GetLoadContext(assembly)!;
            try
            {
                context.LoadFromAssemblyName(new AssemblyName("Acme.Contracts"));
            }
            catch (FileNotFoundException)
            {
                // Load() already ran its conflict handling before returning null; whether the runtime
                // then finds a fallback for this made-up name is irrelevant to what these tests check.
            }

            return Substitute.For<IPluginManifest>();
        }
    }

    private sealed class ConflictingSharedAssemblyResolver(Version hostVersion) : ISharedAssemblyResolver
    {
        public SharedAssemblyDecision Resolve(AssemblyName requested, out Version? host)
        {
            if (string.Equals(requested.Name, "Acme.Contracts", StringComparison.OrdinalIgnoreCase))
            {
                host = hostVersion;
                return SharedAssemblyDecision.Conflict;
            }

            host = null;
            return SharedAssemblyDecision.LoadIsolated;
        }
    }

    private sealed class LocationCapturingManifestLoader : IPluginManifestLoader
    {
        public string? CapturedLocation { get; private set; }

        public IPluginManifest? LoadPluginManifest(Assembly assembly)
        {
            CapturedLocation = assembly.Location;
            return Substitute.For<IPluginManifest>();
        }
    }

    private sealed class ThrowingPluginAssemblyValidator(string throwingAssemblyPath) : IPluginAssemblyValidator
    {
        public PluginAssemblyValidationResult Validate(PluginAssemblyValidationContext context)
            => string.Equals(context.AssemblyPath, throwingAssemblyPath, StringComparison.OrdinalIgnoreCase)
                ? throw new InvalidOperationException("Validator failure by test")
                : PluginAssemblyValidationResult.Accepted();
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
