// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests.AssemblyLoading;

using SAF.PluginSystem.Hosting.AssemblyLoading;

using Microsoft.Extensions.Logging.Abstractions;
using SAF.PluginSystem.Hosting.Contracts;
using System.Reflection;
using Testably.Abstractions;

public class MetadataAssemblyGraphProviderTests
{
    [Fact]
    public void TryResolve_ReturnsNodeWithReferences_ForRealAssembly()
    {
        using var provider = new MetadataAssemblyGraphProvider(NullLogger<MetadataAssemblyGraphProvider>.Instance, new RealFileSystem());
        var target = typeof(IPluginManifest).Assembly.GetName();

        var node = provider.TryResolve(target);

        Assert.NotNull(node);
        Assert.Equal(target.Name, node!.Name.Name);
        Assert.NotNull(node.Name.Version);
        Assert.NotEmpty(node.ReferencedAssemblies);
    }

    [Fact]
    public void TryResolve_ReturnsNull_ForUnknownAssembly()
    {
        using var provider = new MetadataAssemblyGraphProvider(NullLogger<MetadataAssemblyGraphProvider>.Instance, new RealFileSystem());

        var node = provider.TryResolve(new AssemblyName("This.Assembly.Does.Not.Exist"));

        Assert.Null(node);
    }
}
