// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests.AssemblyLoading;

using SAF.PluginSystem.Hosting.AssemblyLoading;

using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SAF.PluginSystem.Hosting.Contracts;
using System.Reflection;

public class SharedAssemblyRegistryTests
{
    private readonly IPublicServiceTypeRegistry _publicServiceTypeRegistry = Substitute.For<IPublicServiceTypeRegistry>();

    public SharedAssemblyRegistryTests()
    {
        _publicServiceTypeRegistry.GetAssemblyNames().Returns([]);
    }

    [Fact]
    public void Closure_IncludesTransitiveContractDependencies()
    {
        _publicServiceTypeRegistry.GetAssemblyNames().Returns(["Acme.Contracts, Version=1.0.0.0"]);

        // Deep transitive chain: Acme.Contracts -> LibX -> LibY -> LibZ.
        var provider = new FakeAssemblyGraphProvider()
            .Add("Acme.Contracts", "1.0.0.0", references: [Name("LibX", "2.0.0.0")])
            .Add("LibX", "2.0.0.0", references: [Name("LibY", "3.0.0.0")])
            .Add("LibY", "3.0.0.0", references: [Name("LibZ", "4.0.0.0")])
            .Add("LibZ", "4.0.0.0");

        var registry = CreateRegistry(provider);

        Assert.True(registry.TryGetSharedAssembly("Acme.Contracts", out var contracts));
        Assert.Equal(new Version(1, 0, 0, 0), contracts.Version);
        Assert.True(registry.TryGetSharedAssembly("LibX", out var libX));
        Assert.Equal(new Version(2, 0, 0, 0), libX.Version);
        Assert.True(registry.TryGetSharedAssembly("LibY", out var libY));
        Assert.Equal(new Version(3, 0, 0, 0), libY.Version);
        Assert.True(registry.TryGetSharedAssembly("LibZ", out var libZ));
        Assert.Equal(new Version(4, 0, 0, 0), libZ.Version);
    }

    [Fact]
    public void Closure_UsesHostResolvedVersion_ForRollForward()
    {
        _publicServiceTypeRegistry.GetAssemblyNames().Returns(["Acme.Contracts, Version=1.0.0.0"]);

        // Contract references LibX 2.0.0.0, but the host provides 2.5.0.0 (roll-forward).
        var provider = new FakeAssemblyGraphProvider()
            .Add("Acme.Contracts", "1.0.0.0", references: [Name("LibX", "2.0.0.0")])
            .Add("LibX", "2.5.0.0");

        var registry = CreateRegistry(provider);

        Assert.True(registry.TryGetSharedAssembly("LibX", out var libX));
        Assert.Equal(new Version(2, 5, 0, 0), libX.Version);
    }

    [Fact]
    public void Closure_SkipsUnresolvableAssemblies()
    {
        _publicServiceTypeRegistry.GetAssemblyNames().Returns(["Acme.Contracts, Version=1.0.0.0"]);

        var provider = new FakeAssemblyGraphProvider()
            .Add("Acme.Contracts", "1.0.0.0", references: [Name("Missing", "1.0.0.0")]);
        // "Missing" is intentionally not registered -> provider returns null.

        var registry = CreateRegistry(provider);

        Assert.True(registry.TryGetSharedAssembly("Acme.Contracts", out _));
        Assert.False(registry.TryGetSharedAssembly("Missing", out _));
    }

    [Fact]
    public void Closure_HandlesCyclesWithoutInfiniteLoop()
    {
        _publicServiceTypeRegistry.GetAssemblyNames().Returns(["Acme.Contracts, Version=1.0.0.0"]);

        var provider = new FakeAssemblyGraphProvider()
            .Add("Acme.Contracts", "1.0.0.0", references: [Name("LibX", "2.0.0.0")])
            .Add("LibX", "2.0.0.0", references: [Name("Acme.Contracts", "1.0.0.0")]);

        var registry = CreateRegistry(provider);

        Assert.True(registry.TryGetSharedAssembly("Acme.Contracts", out _));
        Assert.True(registry.TryGetSharedAssembly("LibX", out _));
    }

    [Fact]
    public void Closure_AlwaysSeedsHostingContractsAssembly()
    {
        var hostingContracts = typeof(IPluginManifest).Assembly.GetName();
        var provider = new FakeAssemblyGraphProvider()
            .Add(hostingContracts.Name!, hostingContracts.Version!.ToString(), hostingContracts.GetPublicKeyToken());

        var registry = CreateRegistry(provider);

        Assert.True(registry.TryGetSharedAssembly(hostingContracts.Name!, out _));
    }

    [Fact]
    public void TryGetSharedAssembly_ReturnsFalse_ForUnknownAssembly()
    {
        var registry = CreateRegistry(new FakeAssemblyGraphProvider());

        Assert.False(registry.TryGetSharedAssembly("Unknown.Assembly", out _));
    }

    [Fact]
    public void FailingClosureBuild_PropagatesException_InsteadOfSilentlyDisablingSharing()
    {
        _publicServiceTypeRegistry.GetAssemblyNames().Returns(["Acme.Contracts, Version=1.0.0.0"]);

        var provider = new ThrowOnceAssemblyGraphProvider(
            new FakeAssemblyGraphProvider().Add("Acme.Contracts", "1.0.0.0"));

        var registry = CreateRegistry(provider);

        // First initialization fails: the error must surface, not be swallowed into an empty shared set.
        Assert.Throws<InvalidOperationException>(() => registry.TryGetSharedAssembly("Acme.Contracts", out _));
    }

    [Fact]
    public void FailingClosureBuild_DoesNotLatchInitialization_AndRecoversOnRetry()
    {
        _publicServiceTypeRegistry.GetAssemblyNames().Returns(["Acme.Contracts, Version=1.0.0.0"]);

        var provider = new ThrowOnceAssemblyGraphProvider(
            new FakeAssemblyGraphProvider().Add("Acme.Contracts", "1.0.0.0"));

        var registry = CreateRegistry(provider);

        Assert.Throws<InvalidOperationException>(() => registry.TryGetSharedAssembly("Acme.Contracts", out _));

        // The failed attempt must not latch _initialized: a subsequent call retries and now succeeds.
        Assert.True(registry.TryGetSharedAssembly("Acme.Contracts", out var contracts));
        Assert.Equal(new Version(1, 0, 0, 0), contracts.Version);
    }

    private SharedAssemblyRegistry CreateRegistry(IAssemblyGraphProvider provider)
        => new(NullLogger<SharedAssemblyRegistry>.Instance, _publicServiceTypeRegistry, provider);

    private static AssemblyName Name(string name, string version, byte[]? publicKeyToken = null)
    {
        var assemblyName = new AssemblyName(name) { Version = Version.Parse(version) };
        if (publicKeyToken is not null)
        {
            assemblyName.SetPublicKeyToken(publicKeyToken);
        }

        return assemblyName;
    }

    private sealed class FakeAssemblyGraphProvider : IAssemblyGraphProvider
    {
        private readonly Dictionary<string, AssemblyGraphNode> _nodes = new(StringComparer.OrdinalIgnoreCase);

        public FakeAssemblyGraphProvider Add(
            string name,
            string version,
            byte[]? publicKeyToken = null,
            IReadOnlyList<AssemblyName>? references = null)
        {
            _nodes[name] = new AssemblyGraphNode(Name(name, version, publicKeyToken), references ?? []);
            return this;
        }

        public AssemblyGraphNode? TryResolve(AssemblyName assemblyName)
            => assemblyName.Name is not null && _nodes.TryGetValue(assemblyName.Name, out var node) ? node : null;
    }

    /// <summary>Throws on the first <see cref="TryResolve"/> call, then delegates to an inner provider.</summary>
    private sealed class ThrowOnceAssemblyGraphProvider(IAssemblyGraphProvider inner) : IAssemblyGraphProvider
    {
        private bool _thrown;

        public AssemblyGraphNode? TryResolve(AssemblyName assemblyName)
        {
            if (!_thrown)
            {
                _thrown = true;
                throw new InvalidOperationException("Simulated closure build failure.");
            }

            return inner.TryResolve(assemblyName);
        }
    }
}
