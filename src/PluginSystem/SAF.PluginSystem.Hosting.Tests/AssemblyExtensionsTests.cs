// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System.Reflection;

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

    [Fact]
    public void LoadPluginManifest_ShouldReturnPluginManifest_WhenAssemblyGetTypesThrowsReflectionTypeLoadException()
    {
        // Arrange
        var assembly = Substitute.For<Assembly>();
        var reflectionTypeLoadException = new ReflectionTypeLoadException(
            [typeof(string), null, typeof(TestPluginManifest)],
            [new TypeLoadException("Type load failed")]);

        assembly.GetTypes().Returns(_ => throw reflectionTypeLoadException);

        // Act
        var result = _manifestLoader.LoadPluginManifest(assembly);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<TestPluginManifest>(result);
    }
}