// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests.AssemblyLoading;

using SAF.PluginSystem.Hosting.AssemblyLoading;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using SAF.PluginSystem.Hosting.Contracts;
using System.IO.Abstractions;
using System.Reflection;
using TestPlugin.PublicDependencyA;

public class SharedAssemblyRegistryTests
{
    private readonly IPublicServiceTypeRegistry _publicServiceTypeRegistry = Substitute.For<IPublicServiceTypeRegistry>();
    private readonly List<ISharedAssemblySource> _sharedAssemblySources = [];

    public SharedAssemblyRegistryTests()
    {
        _publicServiceTypeRegistry.GetAssemblyNames().Returns([]);
    }

    [Theory]
    [InlineData(typeof(IPluginManifest))]        // SAF.PluginSystem.Hosting.Contracts
    [InlineData(typeof(IServiceCollection))]     // Microsoft.Extensions.DependencyInjection.Abstractions
    [InlineData(typeof(IConfiguration))]         // Microsoft.Extensions.Configuration.Abstractions
    [InlineData(typeof(ILoggerFactory))]         // Microsoft.Extensions.Logging.Abstractions
    [InlineData(typeof(IFileSystem))]            // System.IO.Abstractions
    [InlineData(typeof(IOptions<>))]             // Microsoft.Extensions.Options
    [InlineData(typeof(IChangeToken))]           // Microsoft.Extensions.Primitives
    public void SharedSet_AlwaysContains_ImplicitlySharedSafAssembly(Type type)
    {
        var expected = type.Assembly.GetName();

        var registry = CreateRegistry();

        Assert.True(registry.TryGetSharedAssembly(expected.Name!, out var info));
        Assert.Equal(expected.Version, info.Version);
        Assert.Equal(expected.GetPublicKeyToken(), info.PublicKeyToken);
    }

    [Fact]
    public void SharedSet_ContainsConfiguredContractAssemblies_WithVersionAndPublicKeyToken()
    {
        _publicServiceTypeRegistry.GetAssemblyNames()
            .Returns(["Acme.Contracts, Version=2.5.0.0, Culture=neutral, PublicKeyToken=0011223344556677"]);

        var registry = CreateRegistry();

        Assert.True(registry.TryGetSharedAssembly("Acme.Contracts", out var info));
        Assert.Equal(new Version(2, 5, 0, 0), info.Version);
        Assert.Equal(Convert.FromHexString("0011223344556677"), info.PublicKeyToken);
    }

    [Fact]
    public void SharedSet_DoesNotContainTransitiveDependencies_OfContractAssemblies()
    {
        // Only the explicitly configured contract assembly is shared; its (unlisted) dependencies are not.
        _publicServiceTypeRegistry.GetAssemblyNames().Returns(["Acme.Contracts, Version=1.0.0.0"]);

        var registry = CreateRegistry();

        Assert.True(registry.TryGetSharedAssembly("Acme.Contracts", out _));
        Assert.False(registry.TryGetSharedAssembly("System.Text.Json", out _));
    }

    [Fact]
    public void TryGetSharedAssembly_ReturnsFalse_ForUnknownAssembly()
    {
        var registry = CreateRegistry();

        Assert.False(registry.TryGetSharedAssembly("Unknown.Assembly", out _));
    }

    [Fact]
    public void BuildSharedSet_IgnoresMalformedContractName_AndKeepsImplicitAssemblies()
    {
        _publicServiceTypeRegistry.GetAssemblyNames().Returns(["  "]);

        var registry = CreateRegistry();

        Assert.True(registry.TryGetSharedAssembly(typeof(IPluginManifest).Assembly.GetName().Name!, out _));
    }

    [Fact]
    public void GetSharedAssemblies_ReturnsSnapshot_ThatCannotMutateTheRegistry()
    {
        var registry = CreateRegistry();
        var safAssembly = typeof(IPluginManifest).Assembly.GetName().Name!;

        var snapshot = registry.GetSharedAssemblies();
        Assert.True(snapshot.ContainsKey(safAssembly));

        // Mutating the returned collection must not corrupt the registry's shared set.
        ((Dictionary<string, SharedAssemblyInfo>)snapshot).Clear();

        Assert.True(registry.TryGetSharedAssembly(safAssembly, out _));
        Assert.NotSame(snapshot, registry.GetSharedAssemblies());
    }

    [Fact]
    public void TryGetSharedAssembly_BuildsSetOnce_UnderConcurrentFirstAccess()
    {
        _publicServiceTypeRegistry.GetAssemblyNames().Returns(["Acme.Contracts, Version=1.0.0.0"]);

        var registry = CreateRegistry();

        Parallel.For(0, 200, i => Assert.True(registry.TryGetSharedAssembly("Acme.Contracts", out _)));

        _publicServiceTypeRegistry.Received(1).GetAssemblyNames();
    }

    [Fact]
    public void SharedSet_ContainsAssembliesContributedBySources()
    {
        _sharedAssemblySources.Add(new SharedAssemblySource<SharedAssemblyRegistryTests>());
        var expected = typeof(SharedAssemblyRegistryTests).Assembly.GetName();

        var registry = CreateRegistry();

        Assert.True(registry.TryGetSharedAssembly(expected.Name!, out var info));
        Assert.Equal(expected.Version, info.Version);
    }

    [Fact]
    public void SharedSet_ContainsAssembliesFromEverySource()
    {
        _sharedAssemblySources.Add(new StubSharedAssemblySource("First.Contracts, Version=1.0.0.0"));
        _sharedAssemblySources.Add(new StubSharedAssemblySource("Second.Contracts, Version=3.2.0.0"));

        var registry = CreateRegistry();

        Assert.True(registry.TryGetSharedAssembly("First.Contracts", out _));
        Assert.True(registry.TryGetSharedAssembly("Second.Contracts", out var second));
        Assert.Equal(new Version(3, 2, 0, 0), second.Version);
    }

    [Fact]
    public void SharedSet_DerivesVersion_FromAlreadyLoadedAssembly_WhenSourceOmitsIt()
    {
        // A hand-written ISharedAssemblySource can return `new AssemblyName(simpleNameOnly)`, which has no
        // Version - unlike SharedAssemblySource<T>'s typeof(T).Assembly.GetName().
        var expected = typeof(PublicDependencyAMarker).Assembly.GetName();
        _sharedAssemblySources.Add(new StubSharedAssemblySource(expected.Name!));

        var registry = CreateRegistry();

        Assert.True(registry.TryGetSharedAssembly(expected.Name!, out var info));
        Assert.Equal(expected.Version, info.Version);
    }

    [Fact]
    public void SharedSet_WarnsOnceAndIgnoresEntry_WhenSourceOmitsVersion_AndNoLoadedAssemblyMatches()
    {
        _sharedAssemblySources.Add(new StubSharedAssemblySource("Totally.Unresolvable.TestOnlyAssembly"));
        var logger = new CapturingLogger<SharedAssemblyRegistry>();

        var registry = CreateRegistry(logger);

        Assert.False(registry.TryGetSharedAssembly("Totally.Unresolvable.TestOnlyAssembly", out _));
        Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void SharedSet_PrefersTheLoadedSourceVersion_OverAnOnDiskContractAssembly_ForTheSameSimpleName()
    {
        // The contract assembly full name comes from a file on AppContext.BaseDirectory (read via
        // AssemblyName.GetAssemblyName, never loaded); the source reports an already-loaded assembly's own
        // AssemblyName. A stale on-disk copy must not win over the version actually bound in the default
        // context, so the loaded version is kept and the mismatch is logged.
        _sharedAssemblySources.Add(new StubSharedAssemblySource("Acme.Contracts, Version=2.0.0.0"));
        _publicServiceTypeRegistry.GetAssemblyNames().Returns(["Acme.Contracts, Version=1.0.0.0"]);
        var logger = new CapturingLogger<SharedAssemblyRegistry>();

        var registry = CreateRegistry(logger);

        Assert.True(registry.TryGetSharedAssembly("Acme.Contracts", out var info));
        Assert.Equal(new Version(2, 0, 0, 0), info.Version);
        Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void SharedSet_DoesNotWarn_WhenOnDiskContractAssemblyMatchesTheLoadedSourceVersion()
    {
        _sharedAssemblySources.Add(new StubSharedAssemblySource("Acme.Contracts, Version=1.0.0.0"));
        _publicServiceTypeRegistry.GetAssemblyNames().Returns(["Acme.Contracts, Version=1.0.0.0"]);
        var logger = new CapturingLogger<SharedAssemblyRegistry>();

        var registry = CreateRegistry(logger);

        Assert.True(registry.TryGetSharedAssembly("Acme.Contracts", out var info));
        Assert.Equal(new Version(1, 0, 0, 0), info.Version);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void SharedSet_IgnoresThrowingSource_AndKeepsAssembliesFromOtherSources()
    {
        var throwingSource = Substitute.For<ISharedAssemblySource>();
        throwingSource.GetSharedAssemblyNames().Returns(_ => throw new InvalidOperationException("boom"));
        _sharedAssemblySources.Add(throwingSource);
        _sharedAssemblySources.Add(new StubSharedAssemblySource("Good.Contracts, Version=1.0.0.0"));
        var logger = new CapturingLogger<SharedAssemblyRegistry>();

        var registry = CreateRegistry(logger);

        Assert.True(registry.TryGetSharedAssembly("Good.Contracts", out _));
        Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void SharedSet_IgnoresSourceReturningNull_AndKeepsAssembliesFromOtherSources()
    {
        var nullReturningSource = Substitute.For<ISharedAssemblySource>();
        nullReturningSource.GetSharedAssemblyNames().Returns((IEnumerable<AssemblyName>)null!);
        _sharedAssemblySources.Add(nullReturningSource);
        _sharedAssemblySources.Add(new StubSharedAssemblySource("Good.Contracts, Version=1.0.0.0"));
        var logger = new CapturingLogger<SharedAssemblyRegistry>();

        var registry = CreateRegistry(logger);

        Assert.True(registry.TryGetSharedAssembly("Good.Contracts", out _));
        Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void SharedSet_IgnoresNullElementFromSource_AndKeepsOtherAssembliesFromSameSource()
    {
        var sourceWithNullElement = Substitute.For<ISharedAssemblySource>();
        sourceWithNullElement.GetSharedAssemblyNames().Returns(
            new AssemblyName?[] { new("Good.Contracts, Version=1.0.0.0"), null }!);
        _sharedAssemblySources.Add(sourceWithNullElement);
        var logger = new CapturingLogger<SharedAssemblyRegistry>();

        var registry = CreateRegistry(logger);

        Assert.True(registry.TryGetSharedAssembly("Good.Contracts", out _));
        Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void SharedSet_IgnoresThrowingPublicServiceTypeRegistry_AndKeepsAssembliesFromSources()
    {
        _publicServiceTypeRegistry.GetAssemblyNames().Returns(_ => throw new InvalidOperationException("boom"));
        _sharedAssemblySources.Add(new StubSharedAssemblySource("Good.Contracts, Version=1.0.0.0"));
        var logger = new CapturingLogger<SharedAssemblyRegistry>();

        var registry = CreateRegistry(logger);

        Assert.True(registry.TryGetSharedAssembly("Good.Contracts", out _));
        Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    private SharedAssemblyRegistry CreateRegistry()
        => CreateRegistry(NullLogger<SharedAssemblyRegistry>.Instance);

    private SharedAssemblyRegistry CreateRegistry(ILogger<SharedAssemblyRegistry> logger)
        => new(logger, _publicServiceTypeRegistry, _sharedAssemblySources);

    private sealed class StubSharedAssemblySource(params string[] fullNames) : ISharedAssemblySource
    {
        public IEnumerable<AssemblyName> GetSharedAssemblyNames() => fullNames.Select(name => new AssemblyName(name));
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
