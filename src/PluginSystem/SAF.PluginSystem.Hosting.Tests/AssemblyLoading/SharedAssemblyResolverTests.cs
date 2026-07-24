// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests.AssemblyLoading;

using SAF.PluginSystem.Hosting.AssemblyLoading;

using System.Reflection;

public class SharedAssemblyResolverTests
{
    private static readonly byte[] TokenA = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly byte[] TokenB = [8, 7, 6, 5, 4, 3, 2, 1];

    [Fact]
    public void Resolve_ReturnsLoadIsolated_WhenAssemblyNotShared()
    {
        var resolver = CreateResolver(new FakeSharedAssemblyRegistry());

        var decision = resolver.Resolve(Name("NotShared", "1.0.0.0"), out var hostVersion);

        Assert.Equal(SharedAssemblyDecision.LoadIsolated, decision);
        Assert.Null(hostVersion);
    }

    [Fact]
    public void Resolve_ReturnsShareFromDefault_WhenHostVersionIsHigherWithinSameMajor()
    {
        var registry = new FakeSharedAssemblyRegistry().Add("Shared", new Version(1, 5, 0, 0));
        var resolver = CreateResolver(registry);

        var decision = resolver.Resolve(Name("Shared", "1.0.0.0"), out var hostVersion);

        Assert.Equal(SharedAssemblyDecision.ShareFromDefault, decision);
        Assert.Equal(new Version(1, 5, 0, 0), hostVersion);
    }

    [Fact]
    public void Resolve_ReturnsConflict_WhenHostMajorIsHigher_AndMajorRollForwardDisallowed()
    {
        var registry = new FakeSharedAssemblyRegistry().Add("Shared", new Version(2, 0, 0, 0));
        var resolver = CreateResolver(registry, allowMajorVersionRollForward: false);

        var decision = resolver.Resolve(Name("Shared", "1.0.0.0"), out var hostVersion);

        Assert.Equal(SharedAssemblyDecision.Conflict, decision);
        Assert.Equal(new Version(2, 0, 0, 0), hostVersion);
    }

    [Fact]
    public void Resolve_ReturnsShareFromDefault_WhenHostMajorIsHigher_AndMajorRollForwardAllowed()
    {
        var registry = new FakeSharedAssemblyRegistry().Add("Shared", new Version(2, 0, 0, 0));
        var resolver = CreateResolver(registry, allowMajorVersionRollForward: true);

        var decision = resolver.Resolve(Name("Shared", "1.0.0.0"), out _);

        Assert.Equal(SharedAssemblyDecision.ShareFromDefault, decision);
    }

    [Fact]
    public void Resolve_ReturnsShareFromDefault_WhenHostVersionIsEqual()
    {
        var registry = new FakeSharedAssemblyRegistry().Add("Shared", new Version(1, 0, 0, 0));
        var resolver = CreateResolver(registry);

        var decision = resolver.Resolve(Name("Shared", "1.0.0.0"), out _);

        Assert.Equal(SharedAssemblyDecision.ShareFromDefault, decision);
    }

    [Fact]
    public void Resolve_ReturnsConflict_WhenHostVersionIsLower()
    {
        var registry = new FakeSharedAssemblyRegistry().Add("Shared", new Version(1, 0, 0, 0));
        var resolver = CreateResolver(registry);

        var decision = resolver.Resolve(Name("Shared", "2.0.0.0"), out var hostVersion);

        Assert.Equal(SharedAssemblyDecision.Conflict, decision);
        Assert.Equal(new Version(1, 0, 0, 0), hostVersion);
    }

    [Fact]
    public void Resolve_TreatsNullRequestedVersionAsShareable()
    {
        var registry = new FakeSharedAssemblyRegistry().Add("Shared", new Version(1, 0, 0, 0));
        var resolver = CreateResolver(registry);

        var decision = resolver.Resolve(new AssemblyName("Shared"), out _);

        Assert.Equal(SharedAssemblyDecision.ShareFromDefault, decision);
    }

    [Fact]
    public void Resolve_ReturnsShareFromDefault_WhenStrongNameTokensMatch()
    {
        var registry = new FakeSharedAssemblyRegistry().Add("Shared", new Version(1, 5, 0, 0), TokenA);
        var resolver = CreateResolver(registry);

        var decision = resolver.Resolve(Name("Shared", "1.0.0.0", TokenA), out _);

        Assert.Equal(SharedAssemblyDecision.ShareFromDefault, decision);
    }

    [Fact]
    public void Resolve_ReturnsLoadIsolated_WhenPublicKeyTokensDiffer()
    {
        var registry = new FakeSharedAssemblyRegistry().Add("Shared", new Version(2, 0, 0, 0), TokenA);
        var resolver = CreateResolver(registry);

        var decision = resolver.Resolve(Name("Shared", "1.0.0.0", TokenB), out var hostVersion);

        Assert.Equal(SharedAssemblyDecision.LoadIsolated, decision);
        Assert.Null(hostVersion);
    }

    [Fact]
    public void Resolve_ReturnsLoadIsolated_WhenRequestedIsUnsignedButHostIsStrongNamed()
    {
        var registry = new FakeSharedAssemblyRegistry().Add("Shared", new Version(2, 0, 0, 0), TokenA);
        var resolver = CreateResolver(registry);

        var decision = resolver.Resolve(Name("Shared", "1.0.0.0"), out _);

        Assert.Equal(SharedAssemblyDecision.LoadIsolated, decision);
    }

    private static SharedAssemblyResolver CreateResolver(ISharedAssemblyRegistry registry, bool allowMajorVersionRollForward = false)
        => new(registry, new SharedAssemblyVersionComparer(), allowMajorVersionRollForward);

    private static AssemblyName Name(string name, string version, byte[]? publicKeyToken = null)
    {
        var assemblyName = new AssemblyName(name) { Version = Version.Parse(version) };
        if (publicKeyToken is not null)
        {
            assemblyName.SetPublicKeyToken(publicKeyToken);
        }

        return assemblyName;
    }

    private sealed class FakeSharedAssemblyRegistry : ISharedAssemblyRegistry
    {
        private readonly Dictionary<string, SharedAssemblyInfo> _shared = new(StringComparer.OrdinalIgnoreCase);

        public FakeSharedAssemblyRegistry Add(string simpleName, Version version, byte[]? publicKeyToken = null)
        {
            _shared[simpleName] = new SharedAssemblyInfo(version, publicKeyToken);
            return this;
        }

        public bool TryGetSharedAssembly(string simpleName, out SharedAssemblyInfo info)
            => _shared.TryGetValue(simpleName, out info);

        public IReadOnlyDictionary<string, SharedAssemblyInfo> GetSharedAssemblies() => _shared;
    }
}
