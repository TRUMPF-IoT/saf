// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using TestPlugin.PublicDependencyA;
using Testably.Abstractions;
using SAF.PluginSystem.Hosting.AssemblyLoading;
using SAF.PluginSystem.Hosting.Contracts;
using Xunit;

/// <summary>
/// Wires the production <see cref="SharedAssemblyRegistry"/>/<see cref="SharedAssemblyResolver"/> - not a
/// stub - through a real <see cref="PluginAssemblyFolderContainer"/> loading TestPlugin.PluginA/PluginB, to
/// prove the actual sharing decision end to end. <see cref="PluginServicesIsolationTests"/> stays on the
/// stub; it tests service isolation, not assembly sharing.
/// </summary>
public class SharedAssemblyEndToEndTests
{
    private readonly IPluginAssemblyContainer _pluginContainer;

    public SharedAssemblyEndToEndTests()
    {
        var publicServiceTypeRegistry = Substitute.For<IPublicServiceTypeRegistry>();
        publicServiceTypeRegistry.GetAssemblyNames().Returns([typeof(IPublicSingleton).Assembly.FullName!]);

        var registry = new SharedAssemblyRegistry(
            NullLogger<SharedAssemblyRegistry>.Instance,
            publicServiceTypeRegistry,
            [new SharedAssemblySource<IPublicSingleton>()]);
        var resolver = new SharedAssemblyResolver(registry, Options.Create(new PluginSystemOptions()));

        _pluginContainer = new PluginAssemblyFolderContainer(
            NullLoggerFactory.Instance,
            new PluginManifestLoader(),
            new PluginAssemblyFolderSearchOptions
            {
                SearchRootPath = Path.Combine(AppContext.BaseDirectory, "plugins"),
                IncludePatterns = "TestPlugin.Plugin*.dll",
                Recursive = true
            },
            new RealFileSystem(),
            [],
            resolver,
            SharedAssemblyConflictBehavior.Fail);
    }

    [Fact]
    public void RealResolver_LoadsBothTestPlugins()
    {
        var manifests = _pluginContainer.GetPluginManifests().ToList();

        Assert.Equal(2, manifests.Count);
        Assert.Contains(manifests, m => m.GetType().Assembly.GetName().Name == "TestPlugin.PluginA");
        Assert.Contains(manifests, m => m.GetType().Assembly.GetName().Name == "TestPlugin.PluginB");
    }

    [Fact]
    public void RealResolver_SharesThePublicDependency_FromTheDefaultContext()
    {
        var publicDependency = InvokePluginAEntry("GetPublicDependencyAssembly");

        Assert.Same(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(publicDependency));
    }

    [Fact]
    public void RealResolver_LoadsThePluginPrivateDependency_Isolated()
    {
        var privateDependency = InvokePluginAEntry("GetDependencyAssembly");

        Assert.NotSame(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(privateDependency));
    }

    private Assembly InvokePluginAEntry(string methodName)
    {
        var pluginAssembly = _pluginContainer.GetPluginManifests()
            .Single(m => m.GetType().Assembly.GetName().Name == "TestPlugin.PluginA")
            .GetType().Assembly;

        var entryType = pluginAssembly.GetType("TestPlugin.PluginA.PluginAEntry")!;
        var method = entryType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)!;
        return (Assembly)method.Invoke(null, null)!;
    }
}
