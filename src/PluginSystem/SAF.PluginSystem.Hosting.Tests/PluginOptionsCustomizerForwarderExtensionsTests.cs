// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Microsoft.Extensions.DependencyInjection;
using SAF.PluginSystem.Hosting.AssemblyLoading;
using SAF.PluginSystem.Hosting.Contracts;

public class PluginOptionsCustomizerForwarderExtensionsTests
{
    [Fact]
    public void AddPluginOptionsCustomizer_RegistersCustomizerForwarderAndSharedAssemblySource()
    {
        var services = new ServiceCollection();

        services.AddPluginOptionsCustomizer<SampleOptions, SetsName>();

        Assert.Single(services, d => d.ServiceType == typeof(IPluginOptionsCustomizer<SampleOptions>)
                                     && d.ImplementationType == typeof(SetsName));
        Assert.Single(services, d => d.ServiceType == typeof(IHostServiceForwarder)
                                     && d.ImplementationType == typeof(PluginOptionsCustomizerForwarder<SampleOptions>));
        Assert.Single(services, d => d.ServiceType == typeof(ISharedAssemblySource)
                                     && d.ImplementationType == typeof(SharedAssemblySource<IPluginOptionsCustomizer<SampleOptions>>));
    }

    [Fact]
    public void AddPluginOptionsCustomizer_RegistersOneForwarderForSeveralCustomizersOfTheSameOptions()
    {
        var services = new ServiceCollection();

        services.AddPluginOptionsCustomizer<SampleOptions, SetsName>();
        services.AddPluginOptionsCustomizer<SampleOptions, SetsPort>();

        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(IPluginOptionsCustomizer<SampleOptions>)));
        Assert.Single(services, d => d.ServiceType == typeof(IHostServiceForwarder));
        Assert.Single(services, d => d.ServiceType == typeof(ISharedAssemblySource));
    }

    [Fact]
    public void AddPluginOptionsCustomizer_KeepsRegistrationsOfDifferentOptionTypesApart()
    {
        var services = new ServiceCollection();

        services.AddPluginOptionsCustomizer<SampleOptions, SetsName>();
        services.AddPluginOptionsCustomizer<OtherOptions>(o => o.Flag = true);

        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(IHostServiceForwarder)));
        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(ISharedAssemblySource)));
    }

    [Fact]
    public void AddPluginOptionsCustomizer_WithDelegate_RegistersAnInstance()
    {
        var services = new ServiceCollection();

        services.AddPluginOptionsCustomizer<SampleOptions>(o => o.Name = "from delegate");

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IPluginOptionsCustomizer<SampleOptions>));
        var customizer = Assert.IsAssignableFrom<IPluginOptionsCustomizer<SampleOptions>>(descriptor.ImplementationInstance);

        var options = new SampleOptions();
        customizer.Customize(options);
        Assert.Equal("from delegate", options.Name);
    }

    [Fact]
    public void AddPluginOptionsCustomizer_SharesTheAssembliesOfBothTheContractAndTheOptions()
    {
        var names = new SharedAssemblySource<IPluginOptionsCustomizer<SampleOptions>>()
            .GetSharedAssemblyNames()
            .Select(n => n.Name)
            .ToList();

        // The host can only name SampleOptions because it references the plug-in's assembly; without
        // that assembly in the shared set the plug-in would load its own copy and never resolve the
        // forwarded customizer.
        Assert.Contains(typeof(IPluginOptionsCustomizer<>).Assembly.GetName().Name, names);
        Assert.Contains(typeof(SampleOptions).Assembly.GetName().Name, names);
    }

    [Fact]
    public void AddPluginOptionsCustomizer_Throws_WhenServicesIsNull()
        => Assert.Throws<ArgumentNullException>(
            () => ((IServiceCollection)null!).AddPluginOptionsCustomizer<SampleOptions, SetsName>());

    [Fact]
    public void AddPluginOptionsCustomizer_WithDelegate_Throws_WhenCustomizeIsNull()
        => Assert.Throws<ArgumentNullException>(
            () => new ServiceCollection().AddPluginOptionsCustomizer<SampleOptions>(null!));

    public sealed class SampleOptions
    {
        public string? Name { get; set; }
        public int Port { get; set; }
    }

    public sealed class OtherOptions
    {
        public bool Flag { get; set; }
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
