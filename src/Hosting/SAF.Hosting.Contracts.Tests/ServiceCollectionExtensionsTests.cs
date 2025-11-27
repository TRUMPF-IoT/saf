// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Contracts.Tests;

using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddHostedAddsServiceOk()
    {
        var services = new ServiceCollection();
        services.AddHosted<MockHostedService>();

        Assert.Contains(services, sd => sd.ServiceType == typeof(IHostedServiceAsync));
        Assert.Contains(services, sd => sd.ServiceType == typeof(HostedServiceWrapper<MockHostedService>));
        Assert.Contains(services, sd => sd.ServiceType == typeof(MockHostedService));
    }

    [Fact]
    public void AddHostedAsyncAddsServiceOk()
    {
        var services = new ServiceCollection();
        services.AddHostedAsync<MockHostedServiceAsync>();

        Assert.Contains(services, sd => sd.ServiceType == typeof(IHostedServiceAsync));
        Assert.Contains(services, sd => sd.ImplementationType == typeof(MockHostedServiceAsync));
    }

    private class MockHostedService : IHostedService
    {
        public void Start() { }
        public void Stop() { }
        public void Kill() { }
    }

    private class MockHostedServiceAsync : IHostedServiceAsync
    {
        public Task StartAsync(CancellationToken cancelToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancelToken) => Task.CompletedTask;
    }
}