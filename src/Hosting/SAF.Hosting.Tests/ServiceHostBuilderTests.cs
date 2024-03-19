// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Tests;

using Abstractions;
using Common;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

public class ServiceHostBuilderTests
{
    [Fact]
    public void AddsSharedServiceRegistry()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        _ = new ServiceHostBuilder(services);

        // Assert
        Assert.NotEmpty(services);
        Assert.Contains(services, sd => sd.ServiceType == typeof(ISharedServiceRegistry));
    }

    [Fact]
    public void AddServiceHostInfoCallsServiceHostInfoConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ServiceHostBuilder(services);
        builder.ConfigureServiceHostInfo(options => options.Id = "test");

        // Act
        builder.AddServiceHostInfo();

        // Assert
        var info = services.BuildServiceProvider().GetService<IServiceHostInfo>();

        Assert.NotNull(info);
        Assert.Equal("test", info.Id);
    }

    [Fact]
    public void AddServiceHostInfoCreatesDefaultHostId()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ServiceHostBuilder(services);

        // Act
        builder.AddServiceHostInfo();

        // Assert
        var info = services.BuildServiceProvider().GetService<IServiceHostInfo>();

        Assert.NotNull(info);
        Assert.NotEmpty(info.Id);
        Assert.True(Guid.TryParse(info.Id, out _));
    }

    [Fact]
    public void AddServiceHostInfoUsesHostIdFromStorageInfrastructure()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ServiceHostBuilder(services);

        var storage = Substitute.For<IStorageInfrastructure>();
        storage.GetString(Arg.Is("saf/hostid")).Returns("test");
        services.AddSingleton(storage);

        // Act
        builder.AddServiceHostInfo();

        // Assert
        var info = services.BuildServiceProvider().GetService<IServiceHostInfo>();

        Assert.NotNull(info);
        Assert.Equal("test", info.Id);

        storage.Received(1).GetString(Arg.Is("saf/hostid"));
        storage.DidNotReceiveWithAnyArgs().Set(Arg.Any<string>(), Arg.Any<string>());
        storage.DidNotReceiveWithAnyArgs().Set(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        storage.DidNotReceiveWithAnyArgs().Set(Arg.Any<string>(), Arg.Any<byte[]>());
        storage.DidNotReceiveWithAnyArgs().Set(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<byte[]>());
    }

    [Fact]
    public void AddServiceHostInfoCreatesHostIdAndStoresItInStorageInfrastructure()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ServiceHostBuilder(services);

        var storage = Substitute.For<IStorageInfrastructure>();
        storage.GetString(Arg.Any<string>()).Returns((string?)null);
        services.AddSingleton(storage);

        // Act
        builder.AddServiceHostInfo();

        // Assert
        var info = services.BuildServiceProvider().GetService<IServiceHostInfo>();

        Assert.NotNull(info);
        Assert.NotEmpty(info.Id);
        Assert.True(Guid.TryParse(info.Id, out _));

        storage.Received(1).GetString(Arg.Is("saf/hostid"));
        storage.Received(1).Set(Arg.Is("saf/hostid"), Arg.Is(info.Id));
        storage.DidNotReceiveWithAnyArgs().Set(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        storage.DidNotReceiveWithAnyArgs().Set(Arg.Any<string>(), Arg.Any<byte[]>());
        storage.DidNotReceiveWithAnyArgs().Set(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<byte[]>());
    }
}