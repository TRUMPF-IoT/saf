// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Contracts;
using System.IO.Abstractions;
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

        // Act
        var context = new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem);

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

        // Act
        var context = new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem);

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

        // Act
        var context = new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem);

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

        // Act
        var context = new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem);

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

        // Act
        var context = new PluginSystemHostContext(logger, environment, hostConfiguration, options, fileSystem);

        // Assert
        Assert.NotNull(context.HostConfiguration);
        Assert.NotEmpty(context.PluginConfiguration.GetChildren());
        Assert.Equal("Environment", context.PluginConfiguration.GetSection("Key").Value);
    }
}