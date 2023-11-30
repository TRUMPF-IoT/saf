// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SAF.Common;
using SAF.Hosting.Abstractions;
using Xunit;

namespace SAF.Hosting.Tests;

public class ServiceHostTests
{
    private readonly IServiceHostInfo _hostInfo = Substitute.For<IServiceHostInfo>();
    private readonly IConfiguration _configuration = Substitute.For<IConfiguration>();
    private readonly IServiceMessageDispatcher _dispatcher = Substitute.For<IServiceMessageDispatcher>();
    private readonly ISharedServiceRegistry _sharedServiceRegistry = Substitute.For<ISharedServiceRegistry>();
    private readonly IServiceAssemblyManager _serviceAssemblyManager = Substitute.For<IServiceAssemblyManager>();
    private readonly IServiceAssemblyManifest _assemblyManifest = Substitute.For<IServiceAssemblyManifest>();

    private string TestAssemblyPath => Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().Location).LocalPath)!;

    private string TestDataPath => Path.Combine(TestAssemblyPath, "TestData");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartAsyncInitializesStartsAndStopsHostedServices(bool asyncService)
    {
        var service = asyncService ? (IHostedServiceBase)Substitute.For<IHostedServiceAsync>() : Substitute.For<IHostedService>();

        var host = SetupServiceHost(services => services.AddSingleton(service));

        await host.StartAsync(CancellationToken.None);

        _serviceAssemblyManager.Received(1).GetServiceAssemblyManifests();
        _assemblyManifest.Received(1).RegisterDependencies(
            Arg.Any<IServiceCollection>(), 
            Arg.Is<IServiceHostContext>(c => c.HostInfo == _hostInfo && c.Configuration == _configuration));

        if (asyncService)
        {
            await ((IHostedServiceAsync) service).Received(1).StartAsync(Arg.Any<CancellationToken>());
        }
        else
        {
            ((IHostedService) service).Received(1).Start();
        }

        await host.StopAsync(CancellationToken.None);

        if (asyncService)
        {
            await ((IHostedServiceAsync)service).Received(1).StopAsync(Arg.Any<CancellationToken>());
        }
        else
        {
            ((IHostedService)service).Received(1).Stop();
        }
    }

    [Fact]
    public async Task StartAsyncAddsServiceMessageHandlerToMessageDispatcher()
    {
        var messageHandler = new DummyMessageHandler();
        var host = SetupServiceHost(services => services.AddSingleton(messageHandler));
        
        await host.StartAsync(CancellationToken.None);

        _dispatcher.Received(1).AddHandler(Arg.Is(messageHandler.GetType().FullName!), Arg.Any<Func<IMessageHandler>>());
    }

    //[Theory]
    //[InlineData(false)]
    //[InlineData(true)]
    //public async Task DispatcherCallsCorrectHandler(bool asyncService)
    //{
    //    // Arrange
    //    var hostInfo = Substitute.For<IServiceHostInfo>();
    //    var config = Substitute.For<IConfiguration>();
    //    var callCounters = new CallCounters();
    //    var serviceProvider = new ServiceCollection()
    //        .AddSingleton(hostInfo)
    //        .AddSingleton(config)
    //        .BuildServiceProvider();
    //    var serviceAssemblies = new List<IServiceAssemblyManifest> { new CountingTestAssemblyManifest(callCounters, asyncService, true) };
    //    var dispatcher = new ServiceMessageDispatcher(null);

    //    // TODO: using var host = new ServiceHost(serviceProvider, null, dispatcher, serviceAssemblies);
    //    //await host.StartAsync(CancellationToken.None);

    //    // Act
    //    dispatcher.DispatchMessage("SAF.Hosting.Tests.ServiceHostTests+CountingTestHandler", new Message());

    //    // Assert
    //    Assert.Equal(1, callCounters.CanHandleCalled);
    //    Assert.Equal(1, callCounters.HandleCalled);

    //    // TODO: await host.StopAsync(CancellationToken.None);
    //}

    private ServiceHost SetupServiceHost(Action<IServiceCollection> registerDependencies)
    {
        var serviceProvider = new ServiceCollection()
            .AddSingleton(_hostInfo)
            .AddSingleton(_configuration)
            .BuildServiceProvider();

        _assemblyManifest
            .When(m => m.RegisterDependencies(Arg.Any<IServiceCollection>(), Arg.Any<IServiceHostContext>()))
            .Do(ci =>
            {
                var services = ci.Arg<IServiceCollection>();
                registerDependencies(services);
            });

        var serviceAssemblies = new List<IServiceAssemblyManifest> { _assemblyManifest };
        _serviceAssemblyManager.GetServiceAssemblyManifests().Returns(serviceAssemblies);

        return new ServiceHost(NullLogger<ServiceHost>.Instance, serviceProvider, _sharedServiceRegistry, _serviceAssemblyManager, _dispatcher, _configuration);
    }

    private class DummyMessageHandler : IMessageHandler
    {
        public bool CanHandle(Message message) => true;
        public void Handle(Message message) { }
    }
}