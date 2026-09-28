// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Microsoft.Extensions.DependencyInjection;
using SAF.PluginSystem.Hosting.Contracts;

public class PluginOptionsCustomizerForwarderTests
{
    [Fact]
    public void Forward_MakesEveryCustomizerResolvableInThePluginContainer()
    {
        var forwarder = new PluginOptionsCustomizerForwarder<SampleOptions>([new SetsName(), new SetsPort()]);
        var pluginServices = new ServiceCollection();

        forwarder.Forward(pluginServices);

        var customizers = pluginServices.BuildServiceProvider().GetServices<IPluginOptionsCustomizer<SampleOptions>>();
        Assert.Collection(customizers,
            c => Assert.IsType<SetsName>(c),
            c => Assert.IsType<SetsPort>(c));
    }

    [Fact]
    public void Forward_RegistersInstances_SoThePluginContainerDoesNotOwnThem()
    {
        var setsName = new SetsName();
        var forwarder = new PluginOptionsCustomizerForwarder<SampleOptions>([setsName]);
        var pluginServices = new ServiceCollection();

        forwarder.Forward(pluginServices);

        var descriptor = Assert.Single(pluginServices);
        Assert.Equal(typeof(IPluginOptionsCustomizer<SampleOptions>), descriptor.ServiceType);
        Assert.Same(setsName, descriptor.ImplementationInstance);
        Assert.Null(descriptor.ImplementationFactory);
    }

    [Fact]
    public void Forward_AddsNothing_WhenTheHostRegisteredNoCustomizer()
    {
        var forwarder = new PluginOptionsCustomizerForwarder<SampleOptions>([]);
        var pluginServices = new ServiceCollection();

        forwarder.Forward(pluginServices);

        Assert.Empty(pluginServices);
    }

    [Fact]
    public void ApplyPluginOptionsCustomizers_RunsEveryForwardedCustomizerInRegistrationOrder()
    {
        var hostServices = new ServiceCollection();
        hostServices.AddPluginOptionsCustomizer<SampleOptions>(o => o.Name = "first");
        hostServices.AddPluginOptionsCustomizer<SampleOptions>(o => o.Name += " then second");
        hostServices.AddPluginOptionsCustomizer<SampleOptions, SetsPort>();

        var pluginServices = new ServiceCollection();
        foreach (var forwarder in hostServices.BuildServiceProvider().GetServices<IHostServiceForwarder>())
        {
            forwarder.Forward(pluginServices);
        }

        var options = pluginServices.BuildServiceProvider()
            .ApplyPluginOptionsCustomizers(new SampleOptions { Name = "bound from configuration" });

        Assert.Equal("first then second", options.Name);
        Assert.Equal(4242, options.Port);
    }

    [Fact]
    public void ApplyPluginOptionsCustomizers_ReturnsTheOptionsUntouched_WithoutCustomizers()
    {
        var options = new SampleOptions { Name = "bound from configuration" };

        var result = new ServiceCollection().BuildServiceProvider().ApplyPluginOptionsCustomizers(options);

        Assert.Same(options, result);
        Assert.Equal("bound from configuration", result.Name);
    }

    public sealed class SampleOptions
    {
        public string? Name { get; set; }
        public int Port { get; set; }
    }

    public sealed class SetsName : IPluginOptionsCustomizer<SampleOptions>
    {
        public void Customize(SampleOptions options) => options.Name = "set by host";
    }

    public sealed class SetsPort : IPluginOptionsCustomizer<SampleOptions>
    {
        public void Customize(SampleOptions options) => options.Port = 4242;
    }
}
