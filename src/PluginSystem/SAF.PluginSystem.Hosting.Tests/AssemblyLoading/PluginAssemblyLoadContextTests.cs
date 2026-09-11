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

    [Fact]
    public void Conflict_IsolateWithWarning_FallsBackToHostVersion_AndWarns_WhenHostIsHigher_AndPluginShipsNoPrivateCopy()
    {
        var pluginAPath = GetAssemblyPath("TestPlugin.PluginA");
        var capturingLoggerFactory = new CapturingLoggerFactory();

        // The test assembly is loaded in the default context but is not shipped by PluginA, so the plugin's
        // dependency resolver cannot provide a private copy to isolate. The host version is fixed a major
        // above the test assembly's real version, so it is unambiguously the higher one.
        var notShippedByPlugin = typeof(PluginAssemblyLoadContextTests).Assembly.GetName();
        var higherHostVersion = new Version(notShippedByPlugin.Version!.Major + 1, 0, 0, 0);

        var context = new PluginAssemblyLoadContext(
            capturingLoggerFactory,
            pluginAPath,
            new FixedDecisionResolver(notShippedByPlugin.Name!, SharedAssemblyDecision.Conflict, higherHostVersion),
            SharedAssemblyConflictBehavior.IsolateWithWarning);

        var loaded = context.LoadFromAssemblyName(notShippedByPlugin);

        Assert.Same(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(loaded));
        Assert.Contains(capturingLoggerFactory.Entries, e =>
            e.Level == LogLevel.Warning && e.Message.Contains("no private copy") && e.Message.Contains("Falling back to the host version"));
        Assert.DoesNotContain(capturingLoggerFactory.Entries, e => e.Message.Contains("in isolation"));
    }

    [Fact]
    public void Conflict_IsolateWithWarning_WarnsThatBindWillFail_WhenHostIsLower_AndPluginShipsNoPrivateCopy()
    {
        var pluginAPath = GetAssemblyPath("TestPlugin.PluginA");
        var capturingLoggerFactory = new CapturingLoggerFactory();

        // A name nothing provides: not shipped by PluginA (no private copy to isolate) and not loaded
        // anywhere in the default context (the fallback bind itself will fail).
        var requested = new AssemblyName("Not.Shipped.Anywhere") { Version = new Version(2, 0, 0, 0) };
        var lowerHostVersion = new Version(1, 0, 0, 0);

        var context = new PluginAssemblyLoadContext(
            capturingLoggerFactory,
            pluginAPath,
            new FixedDecisionResolver(requested.Name!, SharedAssemblyDecision.Conflict, lowerHostVersion),
            SharedAssemblyConflictBehavior.IsolateWithWarning);

        try
        {
            context.LoadFromAssemblyName(requested);
        }
        catch (FileNotFoundException)
        {
            // Expected: the default context has nothing under this made-up name to bind to.
        }

        Assert.Contains(capturingLoggerFactory.Entries, e =>
            e.Level == LogLevel.Warning && e.Message.Contains("no private copy") && e.Message.Contains("cannot bind the lower host version"));
        Assert.DoesNotContain(capturingLoggerFactory.Entries, e => e.Message.Contains("Falling back to the host version"));
    }

    [Fact]
    public void Conflict_LoadsIsolated_AndLogsError_WhenResolverReportsConflictWithoutHostVersion()
    {
        var pluginAPath = GetAssemblyPath("TestPlugin.PluginA");
        var capturingLoggerFactory = new CapturingLoggerFactory();

        // A misbehaving resolver: reports Conflict without setting hostVersion. ISharedAssemblyResolver
        // only documents this as an expectation - nothing enforces it for a third-party implementation.
        var privateDependency = new AssemblyName("TestPlugin.DependencyA");

        var context = new PluginAssemblyLoadContext(
            capturingLoggerFactory,
            pluginAPath,
            new FixedDecisionResolver(privateDependency.Name!, SharedAssemblyDecision.Conflict, hostVersion: null),
            SharedAssemblyConflictBehavior.Fail);

        var loaded = context.LoadFromAssemblyName(privateDependency);

        Assert.NotSame(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(loaded));
        Assert.Same(context, AssemblyLoadContext.GetLoadContext(loaded));
        Assert.Single(capturingLoggerFactory.Entries, e => e.Level == LogLevel.Error);
        Assert.Empty(context.Conflicts);
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

    private sealed class FixedDecisionResolver(string simpleName, SharedAssemblyDecision decision, Version? hostVersion)
        : ISharedAssemblyResolver
    {
        public SharedAssemblyDecision Resolve(AssemblyName requested, out Version? host)
        {
            if (string.Equals(requested.Name, simpleName, StringComparison.OrdinalIgnoreCase))
            {
                host = hostVersion;
                return decision;
            }

            host = null;
            return SharedAssemblyDecision.LoadIsolated;
        }
    }

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

        public void AddProvider(ILoggerProvider provider) { }

        public void Dispose() { }

        private sealed class CapturingLogger(List<(LogLevel Level, string Message)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => entries.Add((logLevel, formatter(state, exception)));
        }
    }
}