// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Contracts;
using Microsoft.Extensions.DependencyInjection;

public class AssemblyExtensionsTests
{
    private readonly PluginManifestLoader _manifestLoader = new();

    public class TestPluginManifest : IPluginManifest
    {
        public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices) { }
    }

    [Fact]
    public void LoadPluginManifest_ShouldReturnPluginManifest_WhenAssemblyContainsPluginManifest()
    {
        // Arrange
        var assembly = typeof(TestPluginManifest).Assembly;

        // Act
        var result = _manifestLoader.LoadPluginManifest(assembly);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<TestPluginManifest>(result);
    }

    [Fact]
    public void LoadPluginManifest_ShouldReturnNull_WhenAssemblyDoesNotContainPluginManifest()
    {
        // Arrange
        var assembly = typeof(Exception).Assembly;

        // Act
        var result = _manifestLoader.LoadPluginManifest(assembly);

        // Assert
        Assert.Null(result);
    }
}