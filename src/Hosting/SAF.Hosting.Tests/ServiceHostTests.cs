// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Tests;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Common;
using Abstractions;
using Microsoft.Extensions.Logging;
using Xunit;

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
    public async Task StartAsyncInitializesAndStartsHostedServices(bool asyncService)
    {
        var service = asyncService ? (IHostedServiceBase)Substitute.For<IHostedServiceAsync>() : Substitute.For<IHostedService>();

        var host = SetupServiceHost(_ => { }, services => services.AddSingleton(service));

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopAsyncStopsHostedServices(bool asyncService)
    {
        var service = asyncService ? (IHostedServiceBase)Substitute.For<IHostedServiceAsync>() : Substitute.For<IHostedService>();

        var host = SetupServiceHost(_ => { }, services => services.AddSingleton(service));
        await host.StartAsync(CancellationToken.None);

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
    public async Task StartAsyncAddsApplicationMessageHandlerToMessageDispatcher()
    {
        var host = SetupServiceHost(appServices => appServices.AddTransient<DummyMessageHandler>(), _ => { });

        await host.StartAsync(CancellationToken.None);

        _dispatcher.Received(1).AddHandler(Arg.Is(typeof(DummyMessageHandler)), Arg.Any<Func<IMessageHandler>>());
    }

    [Fact]
    public async Task StartAsyncAddsServiceMessageHandlerToMessageDispatcher()
    {
        var host = SetupServiceHost(_ => { }, services => services.AddTransient<DummyMessageHandler>());
        
        await host.StartAsync(CancellationToken.None);

        _dispatcher.Received(1).AddHandler(Arg.Is(typeof(DummyMessageHandler)), Arg.Any<Func<IMessageHandler>>());
    }

    [Fact]
    public async Task StartAsyncRedirectsCommonServices()
    {
        var host = SetupServiceHost(_ => { }, _ => { });

       IServiceCollection? assemblyServices = null;
        _assemblyManifest.When(m => m.RegisterDependencies(Arg.Any<IServiceCollection>(), Arg.Any<IServiceHostContext>()))
            .Do(ci => assemblyServices = ci.Arg<IServiceCollection>());

        await host.StartAsync(CancellationToken.None);

        Assert.NotNull(assemblyServices);
        Assert.Contains(assemblyServices, sd => sd.ServiceType == typeof(IMessagingInfrastructure));
        Assert.Contains(assemblyServices, sd => sd.ServiceType == typeof(IStorageInfrastructure));
        Assert.Contains(assemblyServices, sd => sd.ServiceType == typeof(ILogger));
        Assert.Contains(assemblyServices, sd => sd.ServiceType == typeof(ILogger<>));
        Assert.Contains(assemblyServices, sd => sd.ServiceType == typeof(ILoggerFactory));
        Assert.Contains(assemblyServices, sd => sd.ServiceType == typeof(IServiceHostInfo));
        Assert.Contains(assemblyServices, sd => sd.ServiceType == typeof(IConfiguration));
        Assert.Contains(assemblyServices, sd => sd.ServiceType == typeof(IServiceHostEnvironment));
    }

    [Fact]
    public async Task StartAsyncRedirectsSharedServices()
    {
        var host = SetupServiceHost(_ => { }, _ => { });

        var sharedServices = new ServiceCollection();
        sharedServices.AddSingleton(Substitute.For<IDummySharedService>());
        _sharedServiceRegistry.Services.Returns(sharedServices);

        IServiceCollection? assemblyServices = null;
        _assemblyManifest.When(m => m.RegisterDependencies(Arg.Any<IServiceCollection>(), Arg.Any<IServiceHostContext>()))
            .Do(ci => assemblyServices = ci.Arg<IServiceCollection>());

        await host.StartAsync(CancellationToken.None);

        Assert.NotNull(assemblyServices);
        Assert.Contains(assemblyServices, sd => sd.ServiceType == typeof(IDummySharedService));
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

    private ServiceHost SetupServiceHost(Action<IServiceCollection> registerApplicationDependencies, Action<IServiceCollection> registerServiceDependencies)
    {
        var appServices = new ServiceCollection()
            .AddSingleton(_hostInfo)
            .AddSingleton(_configuration);

        registerApplicationDependencies(appServices);
            
        var serviceProvider = appServices.BuildServiceProvider();

        _assemblyManifest
            .When(m => m.RegisterDependencies(Arg.Any<IServiceCollection>(), Arg.Any<IServiceHostContext>()))
            .Do(ci =>
            {
                var services = ci.Arg<IServiceCollection>();
                registerServiceDependencies(services);
            });

        var serviceAssemblies = new List<IServiceAssemblyManifest> { _assemblyManifest };
        _serviceAssemblyManager.GetServiceAssemblyManifests().Returns(serviceAssemblies);

        var messageHandlerTypes = new ServiceMessageHandlerTypes(appServices);

        return new ServiceHost(NullLogger<ServiceHost>.Instance, serviceProvider, messageHandlerTypes, _sharedServiceRegistry, _serviceAssemblyManager, _dispatcher, _configuration);
    }

    private class DummyMessageHandler : IMessageHandler
    {
        public bool CanHandle(Message message) => true;
        public void Handle(Message message) { }
    }

    public interface IDummySharedService;
}