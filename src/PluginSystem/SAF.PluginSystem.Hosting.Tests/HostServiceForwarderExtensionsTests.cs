// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Microsoft.Extensions.DependencyInjection;
using SAF.PluginSystem.Hosting.AssemblyLoading;
using SAF.PluginSystem.Hosting.Contracts;

public class HostServiceForwarderExtensionsTests
{
    [Fact]
    public void AddHostServiceForwarder_RegistersForwarderAndSharedAssemblySource()
    {
        var services = new ServiceCollection();

        services.AddHostServiceForwarder<ISampleHostService>();

        Assert.Single(services, d => d.ServiceType == typeof(IHostServiceForwarder)
                                     && d.ImplementationType == typeof(HostServiceForwarder<ISampleHostService>));
        Assert.Single(services, d => d.ServiceType == typeof(ISharedAssemblySource)
                                     && d.ImplementationType == typeof(SharedAssemblySource<ISampleHostService>));
    }

    [Fact]
    public void AddHostServiceForwarder_IsIdempotent()
    {
        var services = new ServiceCollection();

        services.AddHostServiceForwarder<ISampleHostService>();
        services.AddHostServiceForwarder<ISampleHostService>();

        Assert.Single(services, d => d.ServiceType == typeof(IHostServiceForwarder));
        Assert.Single(services, d => d.ServiceType == typeof(ISharedAssemblySource));
    }

    [Fact]
    public void AddHostServiceForwarder_KeepsRegistrationsOfDifferentServiceTypes()
    {
        var services = new ServiceCollection();

        services.AddHostServiceForwarder<ISampleHostService>();
        services.AddHostServiceForwarder<IOtherHostService>();

        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(IHostServiceForwarder)));
        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(ISharedAssemblySource)));
    }

    [Fact]
    public void AddHostServiceForwarder_Throws_WhenServicesIsNull()
        => Assert.Throws<ArgumentNullException>(
            () => ((IServiceCollection)null!).AddHostServiceForwarder<ISampleHostService>());

    [Fact]
    public void SharedAssemblySource_ReturnsDeclaringAssemblyOfServiceType()
    {
        var expected = typeof(ISampleHostService).Assembly.GetName();

        var names = new SharedAssemblySource<ISampleHostService>().GetSharedAssemblyNames().ToList();

        Assert.Equal(expected.Name, Assert.Single(names).Name);
    }

    public interface ISampleHostService;

    public interface IOtherHostService;
}
