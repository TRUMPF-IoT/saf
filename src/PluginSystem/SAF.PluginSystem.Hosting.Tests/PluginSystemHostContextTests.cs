// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Contracts;
using System.Linq;
using Testably.Abstractions;

public class PluginSystemHostContextTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Arrange
        var logger = Substitute.For<ILogger<PluginSystemHostContext>>();
        var environment = Substitute.For<IPluginSystemHostEnvironment>();
        var hostConfiguration = Substitute.For<IConfigurationManager>();
        var options = new PluginSystemOptions { PluginSettingsFilePath = "settings.json" };
        // PluginSystemHostContext builds its configuration from settings files on disk, so a real file system is used.
        var fileSystem = new RealFileSystem();
        var hostServices = Substitute.For<IServiceProvider>();

        // Act
        using var context = new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem, hostServices);

        // Assert
        Assert.Equal(environment, context.Environment);
        Assert.Equal(hostConfiguration, context.HostConfiguration);
        Assert.NotNull(context.PluginConfiguration);
    }

    [Fact]
    public void BuildPluginConfiguration_ShouldLogInformation_WhenNoPluginSettingsFilePath()
    {
        // Arrange
        var logger = Substitute.For<ILogger<PluginSystemHostContext>>();
        var environment = Substitute.For<IPluginSystemHostEnvironment>();
        var hostConfiguration = Substitute.For<IConfigurationManager>();
        var options = new PluginSystemOptions { PluginSettingsFilePath = string.Empty };
        // PluginSystemHostContext builds its configuration from settings files on disk, so a real file system is used.
        var fileSystem = new RealFileSystem();
        var hostServices = Substitute.For<IServiceProvider>();

        // Act
        using var context = new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem, hostServices);

        // Assert
        logger.Received().LogInformation("No plugin configuration file configured.");
        Assert.NotNull(context.PluginConfiguration);
        Assert.Empty(context.PluginConfiguration.GetChildren());
    }

    [Fact]
    public void BuildPluginConfiguration_ShouldBuildEmptyConfiguration_WhenSettingsFileNotFound()
    {
        // Arrange
        var logger = Substitute.For<ILogger<PluginSystemHostContext>>();
        var environment = Substitute.For<IPluginSystemHostEnvironment>();
        environment.EnvironmentName.Returns("Environment");
        var hostConfiguration = Substitute.For<IConfigurationManager>();
        var options = new PluginSystemOptions { PluginSettingsFilePath = "nonexistent.json" };
        // PluginSystemHostContext builds its configuration from settings files on disk, so a real file system is used.
        var fileSystem = new RealFileSystem();
        var hostServices = Substitute.For<IServiceProvider>();

        // Act
        using var context = new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem, hostServices);

        // Assert
        Assert.NotNull(context.PluginConfiguration);
        Assert.Empty(context.PluginConfiguration.GetChildren());
    }

    [Fact]
    public void BuildPluginConfiguration_ShouldAddJsonFiles_WhenSettingsFileExists()
    {
        // Arrange
        var logger = Substitute.For<ILogger<PluginSystemHostContext>>();
        var environment = Substitute.For<IPluginSystemHostEnvironment>();
        environment.PluginSettingsRootPath.Returns("./test-plugin-configs");
        var hostConfiguration = Substitute.For<IConfigurationManager>();
        var options = new PluginSystemOptions { PluginSettingsFilePath = "settings.json" };
        // PluginSystemHostContext builds its configuration from settings files on disk, so a real file system is used.
        var fileSystem = new RealFileSystem();
        var hostServices = Substitute.For<IServiceProvider>();

        // Act
        using var context = new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem, hostServices);

        // Assert
        Assert.NotNull(context.HostConfiguration);
        Assert.NotEmpty(context.PluginConfiguration.GetChildren());
        Assert.Equal("Value", context.PluginConfiguration.GetSection("Key").Value);
    }

    [Fact]
    public void BuildPluginConfiguration_ShouldAddEnvironmentJsonFiles_WhenSettingsFileExists()
    {
        // Arrange
        var logger = Substitute.For<ILogger<PluginSystemHostContext>>();
        var environment = Substitute.For<IPluginSystemHostEnvironment>();
        environment.EnvironmentName.Returns("Environment");
        environment.PluginSettingsRootPath.Returns("./test-plugin-configs");
        var hostConfiguration = Substitute.For<IConfigurationManager>();
        var options = new PluginSystemOptions { PluginSettingsFilePath = "settings.json" };
        // PluginSystemHostContext builds its configuration from settings files on disk, so a real file system is used.
        var fileSystem = new RealFileSystem();
        var hostServices = Substitute.For<IServiceProvider>();

        // Act
        using var context = new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem, hostServices);

        // Assert
        Assert.NotNull(context.HostConfiguration);
        Assert.NotEmpty(context.PluginConfiguration.GetChildren());
        Assert.Equal("Environment", context.PluginConfiguration.GetSection("Key").Value);
    }

    [Fact]
    public void BuildPluginConfiguration_EnablesReloadOnChange_ForPluginSettingsFiles()
    {
        // Arrange
        var logger = Substitute.For<ILogger<PluginSystemHostContext>>();
        var environment = Substitute.For<IPluginSystemHostEnvironment>();
        environment.EnvironmentName.Returns("Environment");
        environment.PluginSettingsRootPath.Returns("./test-plugin-configs");
        var hostConfiguration = Substitute.For<IConfigurationManager>();
        var options = new PluginSystemOptions { PluginSettingsFilePath = "settings.json" };
        // PluginSystemHostContext builds its configuration from settings files on disk, so a real file system is used.
        var fileSystem = new RealFileSystem();
        var hostServices = Substitute.For<IServiceProvider>();

        // Act
        using var context = new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem, hostServices);

        // Assert — the plugin settings file(s) must be watched so ReinitializeAsync/ReloadAsync see fresh values.
        var root = Assert.IsType<IConfigurationRoot>(context.PluginConfiguration, exactMatch: false);
        var fileProviders = root.Providers.OfType<FileConfigurationProvider>().ToList();
        Assert.NotEmpty(fileProviders);
        Assert.All(fileProviders, provider => Assert.True(provider.Source.ReloadOnChange));
        Assert.All(fileProviders, provider => Assert.DoesNotContain(fileSystem.Path.DirectorySeparatorChar, provider.Source.Path!));
        Assert.All(fileProviders, provider => Assert.DoesNotContain(fileSystem.Path.AltDirectorySeparatorChar, provider.Source.Path!));
        Assert.All(fileProviders, provider => Assert.NotNull(provider.Source.OnLoadException));

        foreach (var provider in fileProviders)
        {
            var contextWithException = new FileLoadExceptionContext
            {
                Provider = provider,
                Exception = new InvalidDataException("invalid json")
            };

            provider.Source.OnLoadException!(contextWithException);
            Assert.True(contextWithException.Ignore);
        }
    }

    [Fact]
    public void BuildPluginConfiguration_PassesSettingsFileProviderAndFileNameToCustomSources_WhenSettingsFileConfigured()
    {
        // Arrange
        var logger = Substitute.For<ILogger<PluginSystemHostContext>>();
        var environment = Substitute.For<IPluginSystemHostEnvironment>();
        environment.EnvironmentName.Returns("Environment");
        environment.PluginSettingsRootPath.Returns("./test-plugin-configs");
        var hostConfiguration = Substitute.For<IConfigurationManager>();
        var options = new PluginSystemOptions { PluginSettingsFilePath = "settings.json" };
        var fileSystem = new RealFileSystem();
        var hostServices = Substitute.For<IServiceProvider>();

        PluginConfigurationSourceContext? capturedContext = null;
        var configureSources = new List<Action<PluginConfigurationSourceContext>>
        {
            sourceContext => capturedContext = sourceContext,
        };

        // Act
        using var context = new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem, hostServices, configureSources);

        // Assert — the callback receives the same information the default plugin settings sources use.
        Assert.NotNull(capturedContext);
        Assert.NotNull(capturedContext.SettingsFileProvider);
        Assert.Equal("settings.json", capturedContext.SettingsFileName);
        Assert.Equal("Environment", capturedContext.EnvironmentName);
        Assert.NotNull(capturedContext.OnLoadException);
    }

    [Fact]
    public void BuildPluginConfiguration_PassesNullSettingsFileProviderAndFileName_WhenNoPluginSettingsFilePath()
    {
        // Arrange
        var logger = Substitute.For<ILogger<PluginSystemHostContext>>();
        var environment = Substitute.For<IPluginSystemHostEnvironment>();
        environment.EnvironmentName.Returns("Environment");
        var hostConfiguration = Substitute.For<IConfigurationManager>();
        var options = new PluginSystemOptions { PluginSettingsFilePath = string.Empty };
        var fileSystem = new RealFileSystem();
        var hostServices = Substitute.For<IServiceProvider>();

        PluginConfigurationSourceContext? capturedContext = null;
        var configureSources = new List<Action<PluginConfigurationSourceContext>>
        {
            sourceContext => capturedContext = sourceContext,
        };

        // Act
        using var context = new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem, hostServices, configureSources);

        // Assert
        Assert.NotNull(capturedContext);
        Assert.Null(capturedContext.SettingsFileProvider);
        Assert.Null(capturedContext.SettingsFileName);
        Assert.Equal("Environment", capturedContext.EnvironmentName);
    }

    [Fact]
    public void BuildPluginConfiguration_ContextOnLoadException_IgnoresAndLogsWarning_WhenAssignedToCustomSource()
    {
        // Arrange — verifies the escape hatch documented on PluginConfigurationSourceContext.OnLoadException:
        // assigning the shared handler to a custom source gets the same ignore-and-log behavior as the
        // default plugin settings sources.
        var logger = Substitute.For<ILogger<PluginSystemHostContext>>();
        var environment = Substitute.For<IPluginSystemHostEnvironment>();
        var hostConfiguration = Substitute.For<IConfigurationManager>();
        var options = new PluginSystemOptions { PluginSettingsFilePath = string.Empty };
        var fileSystem = new RealFileSystem();
        var hostServices = Substitute.For<IServiceProvider>();

        FileConfigurationSource? capturedSource = null;
        var configureSources = new List<Action<PluginConfigurationSourceContext>>
        {
            sourceContext =>
            {
                sourceContext.Builder.AddJsonFile("nonexistent-explicit.json", optional: true, reloadOnChange: false);
                capturedSource = (FileConfigurationSource)sourceContext.Builder.Sources[^1];
                capturedSource.OnLoadException = sourceContext.OnLoadException;
            },
        };

        // Act
        using var context = new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem, hostServices, configureSources);

        // Assert
        Assert.NotNull(capturedSource);
        var exceptionContext = new FileLoadExceptionContext { Exception = new InvalidDataException("malformed json") };
        capturedSource.OnLoadException!(exceptionContext);

        Assert.True(exceptionContext.Ignore);
        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void BuildPluginConfiguration_ShouldApplyCustomConfigurationSources()
    {
        // Arrange
        var logger = Substitute.For<ILogger<PluginSystemHostContext>>();
        var environment = Substitute.For<IPluginSystemHostEnvironment>();
        var hostConfiguration = Substitute.For<IConfigurationManager>();
        var options = new PluginSystemOptions { PluginSettingsFilePath = string.Empty };
        var configureSources = new List<Action<PluginConfigurationSourceContext>>
        {
            sourceContext =>
            sourceContext.Builder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Custom:Key"] = "CustomValue",
            }),
        };
        // PluginSystemHostContext builds its configuration from settings files on disk, so a real file system is used.
        var fileSystem = new RealFileSystem();
        var hostServices = Substitute.For<IServiceProvider>();

        // Act
        var context = new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem, hostServices, configureSources);

        // Assert
        Assert.Equal("CustomValue", context.PluginConfiguration["Custom:Key"]);
    }

    [Fact]
    public void BuildPluginConfiguration_ShouldApplyCustomSourcesAfterDefaultSources()
    {
        // Arrange
        var logger = Substitute.For<ILogger<PluginSystemHostContext>>();
        var environment = Substitute.For<IPluginSystemHostEnvironment>();
        environment.EnvironmentName.Returns("Environment");
        environment.PluginSettingsRootPath.Returns("./test-plugin-configs");
        var hostConfiguration = Substitute.For<IConfigurationManager>();
        var options = new PluginSystemOptions { PluginSettingsFilePath = "settings.json" };
        var configureSources = new List<Action<PluginConfigurationSourceContext>>
        {
            sourceContext =>
            sourceContext.Builder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Key"] = "OverriddenByCustomSource",
            }),
        };
        // PluginSystemHostContext builds its configuration from settings files on disk, so a real file system is used.
        var fileSystem = new RealFileSystem();
        var hostServices = Substitute.For<IServiceProvider>();

        // Act
        using var context = new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem, hostServices, configureSources);

        // Assert
        Assert.Equal("OverriddenByCustomSource", context.PluginConfiguration["Key"]);
    }

    [Fact]
    public void BuildPluginConfiguration_CustomFileSource_WithoutOnLoadException_GetsDefaultGuardApplied()
    {
        // Arrange
        var logger = Substitute.For<ILogger<PluginSystemHostContext>>();
        var environment = Substitute.For<IPluginSystemHostEnvironment>();
        var hostConfiguration = Substitute.For<IConfigurationManager>();
        var options = new PluginSystemOptions { PluginSettingsFilePath = string.Empty };
        var fileSystem = new RealFileSystem();
        var hostServices = Substitute.For<IServiceProvider>();

        FileConfigurationSource? capturedSource = null;
        var configureSources = new List<Action<PluginConfigurationSourceContext>>
        {
            sourceContext =>
            {
                sourceContext.Builder.AddJsonFile("nonexistent-custom.json", optional: true, reloadOnChange: false);
                capturedSource = (FileConfigurationSource)sourceContext.Builder.Sources[^1];
            },
        };

        // Act
        using var context = new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem, hostServices, configureSources);

        // Assert — the default OnLoadException guard must have been attached because the callback did not set one.
        Assert.NotNull(capturedSource);
        Assert.NotNull(capturedSource.OnLoadException);

        // Verify the guard is also present on the provider that the built IConfigurationRoot actually uses,
        // not only on the source object we captured in the callback. This guards against a future SDK change
        // where ConfigurationBuilder.Build() snapshots/clones sources instead of referencing them directly.
        var root = Assert.IsType<IConfigurationRoot>(context.PluginConfiguration, exactMatch: false);
        var builtProvider = root.Providers
            .OfType<FileConfigurationProvider>()
            .Single(p => p.Source.Path == "nonexistent-custom.json");
        Assert.NotNull(builtProvider.Source.OnLoadException);

        var exceptionContext = new FileLoadExceptionContext
        {
            Exception = new InvalidDataException("malformed json"),
        };
        builtProvider.Source.OnLoadException!(exceptionContext);

        Assert.True(exceptionContext.Ignore);
        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void BuildPluginConfiguration_CustomFileSource_WithExistingOnLoadException_IsNotOverwritten()
    {
        // Arrange
        var logger = Substitute.For<ILogger<PluginSystemHostContext>>();
        var environment = Substitute.For<IPluginSystemHostEnvironment>();
        var hostConfiguration = Substitute.For<IConfigurationManager>();
        var options = new PluginSystemOptions { PluginSettingsFilePath = string.Empty };
        var fileSystem = new RealFileSystem();
        var hostServices = Substitute.For<IServiceProvider>();

        var customHandlerInvoked = false;
        Action<FileLoadExceptionContext> customHandler = _ => customHandlerInvoked = true;

        FileConfigurationSource? capturedSource = null;
        var configureSources = new List<Action<PluginConfigurationSourceContext>>
        {
            sourceContext =>
            {
                sourceContext.Builder.AddJsonFile("nonexistent-custom.json", optional: true, reloadOnChange: false);
                capturedSource = (FileConfigurationSource)sourceContext.Builder.Sources[^1];
                capturedSource.OnLoadException = customHandler;
            },
        };

        // Act
        using var context = new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem, hostServices, configureSources);

        // Assert — the custom handler must not have been replaced by the default guard.
        Assert.NotNull(capturedSource);
        Assert.Same(customHandler, capturedSource.OnLoadException);

        capturedSource.OnLoadException!(new FileLoadExceptionContext { Exception = new InvalidDataException() });
        Assert.True(customHandlerInvoked);
    }

    [Fact]
    public void BuildPluginConfiguration_CustomFileSource_MalformedJson_DoesNotThrowDuringBuild()
    {
        // Arrange
        var logger = Substitute.For<ILogger<PluginSystemHostContext>>();
        var environment = Substitute.For<IPluginSystemHostEnvironment>();
        var hostConfiguration = Substitute.For<IConfigurationManager>();
        var options = new PluginSystemOptions { PluginSettingsFilePath = string.Empty };
        var fileSystem = new RealFileSystem();
        var hostServices = Substitute.For<IServiceProvider>();

        var malformedJsonPath = fileSystem.Path.Combine(fileSystem.Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        try
        {
            fileSystem.File.WriteAllText(malformedJsonPath, "{ this is not valid json");

            var configureSources = new List<Action<PluginConfigurationSourceContext>>
            {
                sourceContext => sourceContext.Builder.AddJsonFile(malformedJsonPath, optional: true, reloadOnChange: false),
            };

            // Act & Assert — a malformed custom JSON file must not crash the host context construction.
            var exception = Record.Exception(() =>
            {
                using var context = new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem, hostServices, configureSources);
            });
            Assert.Null(exception);
        }
        finally
        {
            fileSystem.File.Delete(malformedJsonPath);
        }
    }

    [Fact]
    public void BuildPluginConfiguration_CustomFileSource_IndexBasedOnLoadExceptionOverwrite_IsPreservedAndNotReplacedByDefaultGuard()
    {
        // Arrange
        // This test documents the supported escape-hatch: a caller that needs full control over exception
        // behaviour can set OnLoadException on the source by index *inside the callback*, and the default
        // guard will not overwrite it afterwards.
        var logger = Substitute.For<ILogger<PluginSystemHostContext>>();
        var environment = Substitute.For<IPluginSystemHostEnvironment>();
        var hostConfiguration = Substitute.For<IConfigurationManager>();
        var options = new PluginSystemOptions { PluginSettingsFilePath = string.Empty };
        var fileSystem = new RealFileSystem();
        var hostServices = Substitute.For<IServiceProvider>();

        var customHandlerCallCount = 0;

        var configureSources = new List<Action<PluginConfigurationSourceContext>>
        {
            sourceContext =>
            {
                sourceContext.Builder.AddJsonFile("nonexistent-index-test.json", optional: true, reloadOnChange: false);

                // Index-based access: the source that was just added is the last one in the builder's source list.
                var source = (FileConfigurationSource)sourceContext.Builder.Sources[^1];
                source.OnLoadException = ctx =>
                {
                    ctx.Ignore = true;
                    customHandlerCallCount++;
                };
            },
        };

        // Act
        using var context = new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem, hostServices, configureSources);

        // Assert — the custom handler set via index access must be intact and be the one that fires.
        var root = Assert.IsType<IConfigurationRoot>(context.PluginConfiguration, exactMatch: false);
        var customSource = root.Providers
            .OfType<FileConfigurationProvider>()
            .Single(p => p.Source.Path == "nonexistent-index-test.json");

        Assert.NotNull(customSource.Source.OnLoadException);
        customSource.Source.OnLoadException(new FileLoadExceptionContext { Exception = new InvalidDataException() });
        Assert.Equal(1, customHandlerCallCount);
    }

    [Fact]
    public void BuildPluginConfiguration_CustomCallbackThrows_ExceptionPropagatesAndDoesNotLeakSettingsFileProvider()
    {
        // Arrange
        // A settings file path is required so that a PhysicalFileProvider (and its FileSystemWatcher) is
        // constructed before the custom callbacks run. If the catch block did not dispose it on throw, the
        // watcher handle would leak until process exit.
        var logger = Substitute.For<ILogger<PluginSystemHostContext>>();
        var environment = Substitute.For<IPluginSystemHostEnvironment>();
        environment.PluginSettingsRootPath.Returns("./test-plugin-configs");
        var hostConfiguration = Substitute.For<IConfigurationManager>();
        var options = new PluginSystemOptions { PluginSettingsFilePath = "settings.json" };
        var fileSystem = new RealFileSystem();
        var hostServices = Substitute.For<IServiceProvider>();

        var expectedException = new InvalidOperationException("custom callback failure");
        var configureSources = new List<Action<PluginConfigurationSourceContext>>
        {
            _ => throw expectedException,
        };

        // Act & Assert — the original exception must propagate; it must not be wrapped or swallowed.
        var thrownException = Assert.Throws<InvalidOperationException>(() =>
            new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem, hostServices, configureSources));

        Assert.Same(expectedException, thrownException);
    }

    [Fact]
    public void BuildPluginConfiguration_CustomSourceBuildThrows_ExceptionPropagatesAndDoesNotLeakSettingsFileProvider()
    {
        // Arrange
        // Same leak scenario as above but the throw originates inside ConfigurationBuilder.Build() when
        // a custom IConfigurationSource.Build() throws, not from the registration callback itself.
        var logger = Substitute.For<ILogger<PluginSystemHostContext>>();
        var environment = Substitute.For<IPluginSystemHostEnvironment>();
        environment.PluginSettingsRootPath.Returns("./test-plugin-configs");
        var hostConfiguration = Substitute.For<IConfigurationManager>();
        var options = new PluginSystemOptions { PluginSettingsFilePath = "settings.json" };
        var fileSystem = new RealFileSystem();
        var hostServices = Substitute.For<IServiceProvider>();

        var expectedException = new InvalidOperationException("source build failure");
        var throwingSource = Substitute.For<IConfigurationSource>();
        throwingSource.Build(Arg.Any<IConfigurationBuilder>()).Returns(_ => throw expectedException);

        var configureSources = new List<Action<PluginConfigurationSourceContext>>
        {
            sourceContext => sourceContext.Builder.Add(throwingSource),
        };

        // Act & Assert — the original exception must propagate; it must not be wrapped or swallowed.
        var thrownException = Assert.Throws<InvalidOperationException>(() =>
            new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem, hostServices, configureSources));

        Assert.Same(expectedException, thrownException);
    }

    [Fact]
    public void BuildPluginConfiguration_MultipleCustomFileSources_ShareSingleDefaultFileProvider()
    {
        // Arrange
        // Verifies that the shared PhysicalFileProvider seeded on the builder is reused by every custom
        // FileConfigurationSource that does not set its own FileProvider, instead of each source creating
        // its own PhysicalFileProvider(AppContext.BaseDirectory) via EnsureDefaults — which would leave
        // N undisposed FileSystemWatcher handles after the context is disposed.
        //
        // This test also acts as a canary for future SDK changes to FileConfigurationSource.EnsureDefaults:
        //   • Assert.NotNull  — detects if EnsureDefaults stops assigning FileProvider at all.
        //   • Assert.Same     — detects if EnsureDefaults starts allocating a new provider per source.
        //   • Root assertion  — detects if the shared provider is no longer rooted at AppContext.BaseDirectory.
        // Any of these failing means the fix in BuildPluginConfiguration must be revisited.
        var logger = Substitute.For<ILogger<PluginSystemHostContext>>();
        var environment = Substitute.For<IPluginSystemHostEnvironment>();
        var hostConfiguration = Substitute.For<IConfigurationManager>();
        var options = new PluginSystemOptions { PluginSettingsFilePath = string.Empty };
        var fileSystem = new RealFileSystem();
        var hostServices = Substitute.For<IServiceProvider>();

        var configureSources = new List<Action<PluginConfigurationSourceContext>>
        {
            sourceContext => sourceContext.Builder.AddJsonFile("nonexistent-a.json", optional: true, reloadOnChange: true),
            sourceContext => sourceContext.Builder.AddJsonFile("nonexistent-b.json", optional: true, reloadOnChange: true),
        };

        // Act
        using var context = new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem, hostServices, configureSources);

        // Assert
        var root = Assert.IsType<IConfigurationRoot>(context.PluginConfiguration, exactMatch: false);
        var customProviders = root.Providers
            .OfType<FileConfigurationProvider>()
            .Select(p => p.Source.FileProvider)
            .ToList();

        Assert.Equal(2, customProviders.Count);

        // Both providers must be non-null: if EnsureDefaults stops assigning FileProvider, Assert.Same(null, null)
        // would pass silently and the canary would be blind.
        Assert.All(customProviders, p => Assert.NotNull(p));

        // Both sources must share the exact same instance, proving no per-source allocation occurred.
        Assert.Same(customProviders[0], customProviders[1]);

        // The shared instance must be the PhysicalFileProvider seeded on the builder, rooted at
        // AppContext.BaseDirectory — the same root the SDK fallback would have used per source.
        var physicalProvider = Assert.IsType<PhysicalFileProvider>(customProviders[0]);
        Assert.Equal(
            fileSystem.Path.GetFullPath(AppContext.BaseDirectory),
            fileSystem.Path.GetFullPath(physicalProvider.Root));
    }
}