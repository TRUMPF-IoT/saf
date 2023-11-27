// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SAF.Common;
using Xunit;

namespace SAF.Hosting.Tests;

public class ServiceHostTests
{
    private string TestAssemblyPath => Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().Location).LocalPath)!;

    private string TestDataPath => Path.Combine(TestAssemblyPath, "TestData");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartsAndStopsLoadedServices(bool asyncService)
    {
        // Arrange
        var callCounters = new CallCounters();
        using (var sut = SetupServiceHostWithCallCountersService(callCounters, asyncService))
        {
            // Act
            await sut.StartAsync(CancellationToken.None);

            // Assert
            Assert.Equal(1, callCounters.StartCalled);
            Assert.Equal(0, callCounters.StopCalled);
            Assert.Equal(0, callCounters.KillCalled);

            // Act
            await sut.StopAsync(CancellationToken.None);

            // Assert
            Assert.Equal(1, callCounters.StartCalled);
            Assert.Equal(1, callCounters.StopCalled);
            Assert.Equal(0, callCounters.KillCalled);
        }

        // Assert
        Assert.Equal(1, callCounters.StartCalled);
        Assert.Equal(1, callCounters.StopCalled);
        Assert.Equal(0, callCounters.KillCalled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegistersHandlersWithinDispatchers(bool asyncService)
    {
        // Arrange
        var hostInfo = Substitute.For<IHostInfo>();
        var config = Substitute.For<IConfiguration>();
        var serviceProvider = new ServiceCollection()
            .AddSingleton(hostInfo)
            .AddSingleton(config)
            .BuildServiceProvider();
        var serviceAssemblies = new List<IServiceAssemblyManifest> { new CountingTestAssemblyManifest(new CallCounters(), asyncService, true) };
        var dispatcher = new ServiceMessageDispatcher(null);

        // Act
        using var host = new ServiceHost(serviceProvider, null, dispatcher, serviceAssemblies);
        await host.StartAsync(CancellationToken.None);

        // Assert
        Assert.Single(dispatcher.RegisteredHandlers);
        Assert.Equal("SAF.Hosting.Tests.ServiceHostTests+CountingTestHandler", dispatcher.RegisteredHandlers.First());

        await host.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DispatcherCallsCorrectHandler(bool asyncService)
    {
        // Arrange
        var hostInfo = Substitute.For<IHostInfo>();
        var config = Substitute.For<IConfiguration>();
        var callCounters = new CallCounters();
        var serviceProvider = new ServiceCollection()
            .AddSingleton(hostInfo)
            .AddSingleton(config)
            .BuildServiceProvider();
        var serviceAssemblies = new List<IServiceAssemblyManifest> { new CountingTestAssemblyManifest(callCounters, asyncService, true) };
        var dispatcher = new ServiceMessageDispatcher(null);

        using var host = new ServiceHost(serviceProvider, null, dispatcher, serviceAssemblies);
        await host.StartAsync(CancellationToken.None);
            
        // Act
        dispatcher.DispatchMessage("SAF.Hosting.Tests.ServiceHostTests+CountingTestHandler", new Message());

        // Assert
        Assert.Equal(1, callCounters.CanHandleCalled);
        Assert.Equal(1, callCounters.HandleCalled);

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void SearchingServiceAssembliesWithWrongParameters()
    {
        Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.SearchServiceAssemblies(null!, "**/*.txt", ".*"));
        Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.SearchServiceAssemblies(Path.Combine(TestDataPath, "FilePatterns1"), null!, ".*"));
        Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.SearchServiceAssemblies(Path.Combine(TestDataPath, "FilePatterns1"), "**/*.txt", null!));
    }

    [Fact]
    public void SearchingServiceAssembliesWithSubdirectoryWorks()
    {
        var result = ServiceCollectionExtensions.SearchServiceAssemblies(Path.Combine(TestDataPath, "FilePatterns1"), "**/*.txt", ".*").ToList();
        // All should match -> just compare count
        Assert.Equal(6, result.Count());
    }

    [Fact]
    public void SearchingServiceAssembliesWithoutSubdirectoryWorksCorrectly()
    {
        var result = ServiceCollectionExtensions.SearchServiceAssemblies(Path.Combine(TestDataPath, "FilePatterns1"), "*.txt", ".*").ToList();
            
        Assert.Equal(2, result.Count());
        Assert.Contains(Path.Combine(TestDataPath, "FilePatterns1", "My.Service3.txt"), result);
        Assert.Contains(Path.Combine(TestDataPath, "FilePatterns1", "My.Service3.Contracts.txt"), result);
    }

    [Fact]
    public void SearchingServiceAssembliesWithExclusionGlobWorksCorrectly()
    {
        var result = ServiceCollectionExtensions.SearchServiceAssemblies(Path.Combine(TestDataPath, "FilePatterns1"), "**/My.Service*.txt;|**/*Contracts*.txt", ".*").ToList();

        Assert.Equal(3, result.Count());
        Assert.Contains(Path.Combine(TestDataPath, "FilePatterns1", "My.Service3.txt"), result);
        Assert.Contains(Path.Combine(TestDataPath, "FilePatterns1", "SubDir", "My.Service1.txt"), result);
        Assert.Contains(Path.Combine(TestDataPath, "FilePatterns1", "SubDir", "My.Service2.txt"), result);
    }

    [Fact]
    public void SearchingServiceAssembliesWithFilterPatternWorksCorrectly()
    {
        var result = ServiceCollectionExtensions.SearchServiceAssemblies(Path.Combine(TestDataPath, "FilePatterns1"), "*.txt", "^((?!Contracts).)*$");

        Assert.Single(result);
        Assert.Contains(Path.Combine(TestDataPath, "FilePatterns1", "My.Service3.txt"), result);
    }
        
    private static ServiceHost SetupServiceHostWithCallCountersService(CallCounters callCounters, bool asyncService)
    {
        var hostInfo = Substitute.For<IHostInfo>();
        var config = Substitute.For<IConfiguration>();
        var dispatcher = Substitute.For<IServiceMessageDispatcher>();
        var serviceProvider = new ServiceCollection()
            .AddSingleton(hostInfo)
            .AddSingleton(config)
            .BuildServiceProvider();
        var serviceAssemblies = new List<IServiceAssemblyManifest> { new CountingTestAssemblyManifest(callCounters, asyncService) };
        return new ServiceHost(serviceProvider, null, dispatcher, serviceAssemblies);
    }

    #region internal test miniclasses for "call counting test assembly"

    internal class CallCounters
    {
        public int StartCalled { get; set; }
        public int StopCalled { get; set; }
        public int KillCalled { get; set; }
        public int CanHandleCalled { get; set; }
        public int HandleCalled { get; set; }
    }

    internal class CountingTestAssemblyManifest : IServiceAssemblyManifest
    {
        private readonly CallCounters _counters;
        private readonly bool _registerAsAsyncService;
        private readonly bool _registerAHandler;

        public CountingTestAssemblyManifest(CallCounters counters, bool registerAsAsyncService, bool registerAHandler = false)
        {
            _counters = counters;
            _registerAsAsyncService = registerAsAsyncService;
            _registerAHandler = registerAHandler;
        }

        public string FriendlyName => "Internal test service - only for this test";

        public void RegisterDependencies(IServiceCollection services, IServiceHostContext context)
        {
            if(!_registerAsAsyncService)
                services.AddHosted<SyncCountingTestService>();
            else
                services.AddHostedAsync<AsyncCountingTestService>();
                
            services.AddSingleton(typeof(CallCounters), r => _counters);

            if(_registerAHandler)
                services.AddTransient<CountingTestHandler>();
        }
    }

    internal class SyncCountingTestService : IHostedService
    {
        private readonly CallCounters _counters;
        public SyncCountingTestService(CallCounters counters) => _counters = counters ?? new CallCounters();
        public void Start() => _counters.StartCalled++;
        public void Stop() => _counters.StopCalled++;
        public void Kill() => _counters.KillCalled++;
    }

    internal class AsyncCountingTestService : IHostedServiceAsync
    {
        private readonly CallCounters _counters;
        public AsyncCountingTestService(CallCounters counters) => _counters = counters ?? new CallCounters();

        public void Start() => _counters.StartCalled++;
        public void Stop() => _counters.StopCalled++;

        public Task StartAsync(CancellationToken _)
        {
            Start();
            return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken _)
        {
            Stop();
            return Task.CompletedTask;
        }
    }

    internal class CountingTestHandler : IMessageHandler
    {
        private readonly CallCounters _counters;
        public CountingTestHandler(CallCounters counters) => _counters = counters ?? new CallCounters();
        public bool CanHandle(Message message) => _counters.CanHandleCalled++ > -1;
        public void Handle(Message message) => _counters.HandleCalled++;
    }

    #endregion
}