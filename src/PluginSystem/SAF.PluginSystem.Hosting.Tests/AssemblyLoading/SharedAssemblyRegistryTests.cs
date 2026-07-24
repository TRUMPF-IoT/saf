// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests.AssemblyLoading;

using SAF.PluginSystem.Hosting.AssemblyLoading;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SAF.PluginSystem.Hosting.Contracts;
using System.IO.Abstractions;

public class SharedAssemblyRegistryTests
{
    private readonly IPublicServiceTypeRegistry _publicServiceTypeRegistry = Substitute.For<IPublicServiceTypeRegistry>();

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

    private SharedAssemblyRegistry CreateRegistry()
        => new(NullLogger<SharedAssemblyRegistry>.Instance, _publicServiceTypeRegistry);
}
