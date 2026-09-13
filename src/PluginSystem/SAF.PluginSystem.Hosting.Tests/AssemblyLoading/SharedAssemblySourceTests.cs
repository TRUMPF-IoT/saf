// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests.AssemblyLoading;

using SAF.PluginSystem.Hosting.AssemblyLoading;
using TestPlugin.PublicDependencyA;

public class SharedAssemblySourceTests
{
    [Fact]
    public void GetSharedAssemblyNames_ReturnsOnlyTheDeclaringAssembly_ForANonGenericType()
    {
        var source = new SharedAssemblySource<IPublicTransient>();

        var name = Assert.Single(source.GetSharedAssemblyNames());

        Assert.Equal(typeof(IPublicTransient).Assembly.GetName().Name, name.Name);
    }

    [Fact]
    public void GetSharedAssemblyNames_AlsoReturnsTheTypeArgumentAssembly_ForAConstructedGenericType()
    {
        var source = new SharedAssemblySource<GenericHolder<IPublicTransient>>();

        var names = source.GetSharedAssemblyNames().Select(n => n.Name).ToList();

        Assert.Contains(typeof(GenericHolder<>).Assembly.GetName().Name, names);
        Assert.Contains(typeof(IPublicTransient).Assembly.GetName().Name, names);
    }

    [Fact]
    public void GetSharedAssemblyNames_ReturnsEachAssemblyOnlyOnce_WhenTheSameAssemblyRecursesTwice()
    {
        var source = new SharedAssemblySource<GenericHolder<GenericHolder<IPublicTransient>>>();

        var names = source.GetSharedAssemblyNames().Select(n => n.Name).ToList();

        Assert.Equal(2, names.Count);
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    private sealed class GenericHolder<TArgument>
    {
    }
}
