// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using System.Reflection;
using System.Runtime.Loader;
using Testably.Abstractions;
using Xunit.Abstractions;

public class PluginAssemblyLoadContextTests
{
    private readonly ILoggerFactory _loggerFactory;

    // This test loads a real plugin assembly from disk through AssemblyLoadContext, which reads from
    // the real file system, so a mock cannot be used.
    private readonly IFileSystem _fileSystem = new RealFileSystem();

    public PluginAssemblyLoadContextTests(ITestOutputHelper outputHelper)
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddXunit(outputHelper, LogLevel.Trace).SetMinimumLevel(LogLevel.Trace));
    }

    [Fact]
    public void LoadsAssemblyInPluginContext()
    {
        // Arrange
        var pluginAPath = GetAssemblyPath("TestPlugin.PluginA");

        var context = new PluginAssemblyLoadContext(
            _loggerFactory,
            pluginAPath,
            _fileSystem);

        // Act
        var pluginA = context.LoadFromAssemblyPath(pluginAPath);

        // Assert
        Assert.NotNull(pluginA);
        var pluginAContext = AssemblyLoadContext.GetLoadContext(pluginA);
        Assert.Same(context, pluginAContext);
        Assert.NotSame(AssemblyLoadContext.Default, pluginAContext);
    }

    [Fact]
    public void LoadsAssembliesInPluginContexts()
    {
        // Arrange
        var pluginAPath = GetAssemblyPath("TestPlugin.PluginA");
        var pluginBPath = GetAssemblyPath("TestPlugin.PluginB");

        var contextA = new PluginAssemblyLoadContext(
            _loggerFactory,
            pluginAPath,
            _fileSystem);
        var contextB = new PluginAssemblyLoadContext(
            _loggerFactory,
            pluginBPath,
            _fileSystem);

        // Act
        var pluginA = contextA.LoadFromAssemblyPath(pluginAPath);
        var pluginB = contextB.LoadFromAssemblyPath(pluginBPath);

        // Assert
        Assert.NotNull(pluginA);
        var pluginAContext = AssemblyLoadContext.GetLoadContext(pluginA);
        Assert.Same(contextA, pluginAContext);
        Assert.NotSame(AssemblyLoadContext.Default, pluginAContext);

        Assert.NotNull(pluginB);
        var pluginBContext = AssemblyLoadContext.GetLoadContext(pluginB);
        Assert.Same(contextB, pluginBContext);
        Assert.NotSame(AssemblyLoadContext.Default, pluginBContext);
    }

    [Fact]
    public void LoadsAssemblyDependencyInPluginContext()
    {
        // Arrange
        var pluginAPath = GetAssemblyPath("TestPlugin.PluginA");

        var context = new PluginAssemblyLoadContext(
            _loggerFactory,
            pluginAPath,
            _fileSystem);

        // Act
        var pluginA = context.LoadFromAssemblyPath(pluginAPath);
        var pluginADepA = GetDependencyAssembly(pluginA, "TestPlugin.PluginA.PluginAEntry");

        // Assert
        Assert.NotNull(pluginADepA);
        var pluginADepContext = AssemblyLoadContext.GetLoadContext(pluginADepA);
        Assert.Same(context, pluginADepContext);
        Assert.NotSame(AssemblyLoadContext.Default, pluginADepContext);
    }

    [Fact]
    public void LoadsTransitiveAssemblyDependencyInPluginContext()
    {
        // Arrange
        var pluginBPath = GetAssemblyPath("TestPlugin.PluginB");

        var context = new PluginAssemblyLoadContext(
            _loggerFactory,
            pluginBPath,
            _fileSystem);

        // Act
        var pluginB = context.LoadFromAssemblyPath(pluginBPath);
        var pluginBTransDepB = GetTransitiveDependencyAssembly(pluginB, "TestPlugin.PluginB.PluginBEntry");

        // Assert
        Assert.NotNull(pluginBTransDepB);
        var pluginBDepContext = AssemblyLoadContext.GetLoadContext(pluginBTransDepB);
        Assert.Same(context, pluginBDepContext);
        Assert.NotSame(AssemblyLoadContext.Default, pluginBDepContext);
    }

    [Fact]
    public void LoadsAssemblyPublicDependencyInDefaultContext()
    {
        // Arrange
        var pluginAPath = GetAssemblyPath("TestPlugin.PluginA");

        var context = new PluginAssemblyLoadContext(
            _loggerFactory,
            pluginAPath,
            _fileSystem);

        // Act
        var pluginA = context.LoadFromAssemblyPath(pluginAPath);
        var pluginADepA = GetPublicDependencyAssembly(pluginA, "TestPlugin.PluginA.PluginAEntry");

        // Assert
        Assert.NotNull(pluginADepA);
        var pluginADepContext = AssemblyLoadContext.GetLoadContext(pluginADepA);
        Assert.NotSame(context, pluginADepContext);
        Assert.Same(AssemblyLoadContext.Default, pluginADepContext);
    }

    [Fact]
    public void LoadsTransitiveAssemblyPublicDependencyInDefaultContext()
    {
        // Arrange
        var pluginBPath = GetAssemblyPath("TestPlugin.PluginB");

        var context = new PluginAssemblyLoadContext(
            _loggerFactory,
            pluginBPath,
            _fileSystem);

        // Act
        var pluginB = context.LoadFromAssemblyPath(pluginBPath);
        var pluginBTransDepB = GetTransitivePublicDependencyAssembly(pluginB, "TestPlugin.PluginB.PluginBEntry");

        // Assert
        Assert.NotNull(pluginBTransDepB);
        var pluginBDepContext = AssemblyLoadContext.GetLoadContext(pluginBTransDepB);
        Assert.NotSame(context, pluginBDepContext);
        Assert.Same(AssemblyLoadContext.Default, pluginBDepContext);
    }

    private static string GetAssemblyPath(string pluginName)
        => Path.Combine(AppContext.BaseDirectory, "plugins", pluginName, $"{pluginName}.dll");

    private static Assembly GetDependencyAssembly(Assembly assembly, string typeName)
        => GetAssemblyDependency(assembly, typeName, nameof(GetDependencyAssembly))!;

    private static Assembly GetTransitiveDependencyAssembly(Assembly assembly, string typeName)
        => GetAssemblyDependency(assembly, typeName, nameof(GetTransitiveDependencyAssembly))!;

    private static Assembly GetPublicDependencyAssembly(Assembly assembly, string typeName)
        => GetAssemblyDependency(assembly, typeName, nameof(GetPublicDependencyAssembly))!;

    private static Assembly GetTransitivePublicDependencyAssembly(Assembly assembly, string typeName)
        => GetAssemblyDependency(assembly, typeName, nameof(GetTransitivePublicDependencyAssembly))!;

    private static Assembly? GetAssemblyDependency(Assembly assembly, string typeName, string methodName)
    {
        var type = assembly.GetType(typeName)!;
        var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public)!;
        return method.Invoke(null, null) as Assembly;
    }
}