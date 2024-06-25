// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Tests;

using Contracts;
using Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddHostCoreAddsCoreServices()
    {
        var services = new ServiceCollection();
        var builder = services.AddHostCore();

        Assert.Contains(services, s => s.ServiceType == typeof(IServiceAssemblyManager));
        Assert.Contains(services, s => s.ServiceType == typeof(IServiceMessageDispatcher));
        Assert.Contains(services, s => s.ServiceType == typeof(ServiceHost));
        Assert.Contains(services, s => s.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService));
        Assert.Contains(services, s => s.ServiceType == typeof(IServiceHostInfo));
        Assert.Contains(services, s => s.ServiceType == typeof(IServiceMessageHandlerTypes));

        Assert.Equal(services, builder.Services);
    }

    [Fact]
    public void AddHostBuildsDefaultHost()
    {
        var services = new ServiceCollection();
        var builder = services.AddHost();

        Assert.Contains(services, s => s.ServiceType == typeof(IServiceAssemblySearch));
        Assert.Contains(services, s => s.ServiceType == typeof(IConfigureOptions<ServiceAssemblySearchOptions>));

        Assert.Equal(services, builder.Services);
    }
}