// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests.AssemblyLoading;

using SAF.PluginSystem.Hosting.AssemblyLoading;

using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Runtime.Loader;
using Xunit;

public class PluginAssemblyLoadContextTests
{
    private readonly ILoggerFactory _loggerFactory;

    // Shares the contract closure (hosting contracts, Microsoft.Extensions.* and public dependencies);
    // private plugin dependencies stay isolated.
    private readonly ISharedAssemblyResolver _sharedAssemblyResolver = TestSharedAssemblyResolver.SharesHostProvidedAssemblies;

    public PluginAssemblyLoadContextTests(ITestOutputHelper outputHelper)
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddXUnit(outputHelper).SetMinimumLevel(LogLevel.Trace));
    }

    [Fact]
    public void LoadsAssemblyInPluginContext()
    {
        // Arrange
        var pluginAPath = GetAssemblyPath("TestPlugin.PluginA");

        var context = new PluginAssemblyLoadContext(
            _loggerFactory,
            pluginAPath,
            _sharedAssemblyResolver,
            SharedAssemblyConflictBehavior.Fail);

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
            _sharedAssemblyResolver,
            SharedAssemblyConflictBehavior.Fail);
        var contextB = new PluginAssemblyLoadContext(
            _loggerFactory,
            pluginBPath,
            _sharedAssemblyResolver,
            SharedAssemblyConflictBehavior.Fail);

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
            _sharedAssemblyResolver,
            SharedAssemblyConflictBehavior.Fail);

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
            _sharedAssemblyResolver,
            SharedAssemblyConflictBehavior.Fail);

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
            _sharedAssemblyResolver,
            SharedAssemblyConflictBehavior.Fail);

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
            _sharedAssemblyResolver,
            SharedAssemblyConflictBehavior.Fail);

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