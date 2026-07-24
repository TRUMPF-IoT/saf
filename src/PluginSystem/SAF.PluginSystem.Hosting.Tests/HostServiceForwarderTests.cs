// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class HostServiceForwarderTests
{
    [Fact]
    public void Forward_RegistersInstanceInPluginServices()
    {
        var instance = new StubService();
        var forwarder = new HostServiceForwarder<StubService>(instance);
        var pluginServices = new ServiceCollection();

        forwarder.Forward(pluginServices);

        var resolved = pluginServices.BuildServiceProvider().GetRequiredService<StubService>();
        Assert.Same(instance, resolved);
    }

    [Fact]
    public void Forward_RegistersSameInstanceAcrossMultiplePluginContainers()
    {
        var instance = new StubService();
        var forwarder = new HostServiceForwarder<StubService>(instance);

        var servicesA = new ServiceCollection();
        var servicesB = new ServiceCollection();
        forwarder.Forward(servicesA);
        forwarder.Forward(servicesB);

        Assert.Same(instance, servicesA.BuildServiceProvider().GetRequiredService<StubService>());
        Assert.Same(instance, servicesB.BuildServiceProvider().GetRequiredService<StubService>());
    }

    [Fact]
    public void Forward_RegisteredServiceIsResolvableAsSingleton()
    {
        var instance = new StubService();
        var forwarder = new HostServiceForwarder<StubService>(instance);
        var pluginServices = new ServiceCollection();

        forwarder.Forward(pluginServices);

        var provider = pluginServices.BuildServiceProvider();
        var first = provider.GetRequiredService<StubService>();
        var second = provider.GetRequiredService<StubService>();
        Assert.Same(first, second);
    }

    private sealed class StubService;
}
