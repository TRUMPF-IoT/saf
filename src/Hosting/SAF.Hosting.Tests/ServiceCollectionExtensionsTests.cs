// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SAF.Common;
using SAF.PluginSystem.Hosting;
using SAF.PluginSystem.Hosting.Contracts;
using Xunit;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddServiceHostInfo_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        IServiceCollection? services = null;

        Assert.Throws<ArgumentNullException>(() => services!.AddServiceHostInfo(static _ => { }));
    }

    [Fact]
    public void AddServiceHostInfo_WhenConfigurationIsNull_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddServiceHostInfo(null!));
    }

    [Fact]
    public void AddServiceHostInfo_WhenStorageContainsId_UsesExistingId()
    {
        var storage = Substitute.For<IStorageInfrastructure>();
        storage.GetString("saf/hostid").Returns("existing-host-id");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceHost:ServiceHostType"] = "UnitTest",
                ["ServiceHost:FileSystemUserBasePath"] = "user-path",
                ["ServiceHost:FileSystemInstallationPath"] = "install-path"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(storage);
        services.AddServiceHostInfo(configuration.GetSection("ServiceHost").Bind);

        var provider = services.BuildServiceProvider();
        var hostInfo = provider.GetRequiredService<IServiceHostInfo>();

        Assert.Equal("existing-host-id", hostInfo.Id);
        Assert.Equal("UnitTest", hostInfo.ServiceHostType);
        Assert.Equal("user-path", hostInfo.FileSystemUserBasePath);
        Assert.Equal("install-path", hostInfo.FileSystemInstallationPath);
        storage.DidNotReceive().Set("saf/hostid", Arg.Any<string>());
    }

    [Fact]
    public void AddServiceHostInfo_WhenStorageIdMissing_GeneratesAndPersistsId()
    {
        var storage = Substitute.For<IStorageInfrastructure>();
        var configuration = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        services.AddSingleton(storage);
        services.AddServiceHostInfo(configuration.GetSection("ServiceHost").Bind);

        var provider = services.BuildServiceProvider();
        var hostInfo = provider.GetRequiredService<IServiceHostInfo>();

        Assert.False(string.IsNullOrWhiteSpace(hostInfo.Id));
        storage.Received(1).Set("saf/hostid", Arg.Is<string>(value => !string.IsNullOrWhiteSpace(value)));
    }

    [Fact]
    public void AddServiceHostInfo_WhenRootStorageMissing_UsesPluginStorageContainingExistingId()
    {
        var storage = Substitute.For<IStorageInfrastructure>();
        storage.GetString("saf/hostid").Returns("plugin-host-id");

        var pluginServiceProvider = Substitute.For<IPluginServiceProvider>();
        pluginServiceProvider.GetService<IStorageInfrastructure>().Returns(storage);

        var services = new ServiceCollection();
        services.AddSingleton(pluginServiceProvider);
        services.AddServiceHostInfo(static _ => { });

        var provider = services.BuildServiceProvider();
        var hostInfo = provider.GetRequiredService<IServiceHostInfo>();

        Assert.Equal("plugin-host-id", hostInfo.Id);
        storage.DidNotReceive().Set("saf/hostid", Arg.Any<string>());
    }

    [Fact]
    public void AddServiceHostInfo_WhenRootStorageMissing_PersistsIdToPluginStorage()
    {
        var storage = Substitute.For<IStorageInfrastructure>();
        var pluginServiceProvider = Substitute.For<IPluginServiceProvider>();
        pluginServiceProvider.GetService<IStorageInfrastructure>().Returns(storage);

        var services = new ServiceCollection();
        services.AddSingleton(pluginServiceProvider);
        services.AddServiceHostInfo(static _ => { });

        var provider = services.BuildServiceProvider();
        var hostInfo = provider.GetRequiredService<IServiceHostInfo>();

        Assert.False(string.IsNullOrWhiteSpace(hostInfo.Id));
        storage.Received(1).Set("saf/hostid", Arg.Is<string>(value => !string.IsNullOrWhiteSpace(value)));
    }

    [Fact]
    public void AddServiceHostInfo_WhenUsingConfiguration_BindsConfiguredValues()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceHost:Id"] = "configured-id",
                ["ServiceHost:ServiceHostType"] = "ConfiguredType",
                ["ServiceHost:FileSystemUserBasePath"] = "configured-user",
                ["ServiceHost:FileSystemInstallationPath"] = "configured-install"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddServiceHostInfo(configuration.GetSection("ServiceHost").Bind);

        var provider = services.BuildServiceProvider();
        var hostInfo = provider.GetRequiredService<IServiceHostInfo>();

        Assert.Equal("configured-id", hostInfo.Id);
        Assert.Equal("ConfiguredType", hostInfo.ServiceHostType);
        Assert.Equal("configured-user", hostInfo.FileSystemUserBasePath);
        Assert.Equal("configured-install", hostInfo.FileSystemInstallationPath);
    }

    [Fact]
    public void AddServiceHostInfo_RegistersHostServiceForwarderForIServiceHostInfo()
    {
        var services = new ServiceCollection();

        services.AddServiceHostInfo(static _ => { });

        var provider = services.BuildServiceProvider();
        var forwarder = provider.GetRequiredService<IHostServiceForwarder>();
        Assert.IsType<HostServiceForwarder<IServiceHostInfo>>(forwarder);
    }

    [Fact]
    public void AddServiceHostInfo_ForwarderMakesIServiceHostInfoResolvableInPluginContainer()
    {
        var services = new ServiceCollection();
        services.AddServiceHostInfo(opts => opts.ServiceHostType = "ForwardedType");

        var hostProvider = services.BuildServiceProvider();
        var forwarder = hostProvider.GetRequiredService<IHostServiceForwarder>();

        var pluginServices = new ServiceCollection();
        forwarder.Forward(pluginServices);

        var pluginHostInfo = pluginServices.BuildServiceProvider().GetRequiredService<IServiceHostInfo>();
        Assert.Equal("ForwardedType", pluginHostInfo.ServiceHostType);
    }

    [Fact]
    public void AddServiceHostInfo_ForwarderForwardsSameInstanceAsHostContainer()
    {
        var services = new ServiceCollection();
        services.AddServiceHostInfo(static _ => { });

        var hostProvider = services.BuildServiceProvider();
        var hostInfo = hostProvider.GetRequiredService<IServiceHostInfo>();

        var forwarder = hostProvider.GetRequiredService<IHostServiceForwarder>();
        var pluginServices = new ServiceCollection();
        forwarder.Forward(pluginServices);

        var pluginHostInfo = pluginServices.BuildServiceProvider().GetRequiredService<IServiceHostInfo>();
        Assert.Same(hostInfo, pluginHostInfo);
    }

    [Fact]
    public void AddServiceHostInfo_ForwarderHonorsStackedCodeBasedConfigureOverride()
    {
        var services = new ServiceCollection();
        services.AddServiceHostInfo(opts => opts.ServiceHostType = "FromConfig");
        services.Configure<ServiceHostOptions>(opts => opts.ServiceHostType = "FromCode");

        var hostProvider = services.BuildServiceProvider();
        var forwarder = hostProvider.GetRequiredService<IHostServiceForwarder>();

        var pluginServices = new ServiceCollection();
        forwarder.Forward(pluginServices);

        var pluginHostInfo = pluginServices.BuildServiceProvider().GetRequiredService<IServiceHostInfo>();
        Assert.Equal("FromCode", pluginHostInfo.ServiceHostType);
    }
}
