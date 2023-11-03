// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SAF.Common;
using Xunit;

namespace SAF.Hosting.Tests;

[CollectionDefinition("DiRedirectionTests", DisableParallelization = true)] // uses static stuff...
public class DiRedirectionTests
{
    public static int DummyMessagingHashCode;
    public static int DummyStorageHashCode;

    [Fact]
    public async Task OuterServicesRedirected()
    {
        var hostInfo = Substitute.For<IHostInfo>();

        var dummyMessagingImplementation = new DummyMessaging();
        DummyMessagingHashCode = dummyMessagingImplementation.GetHashCode();

        var dummyStorageImplementation = new DummyStorage();
        DummyStorageHashCode = dummyStorageImplementation.GetHashCode();

        var serviceProvider = new ServiceCollection()
            .AddSingleton(hostInfo)
            .AddSingleton<IMessagingInfrastructure>(r => dummyMessagingImplementation)
            .AddSingleton<IStorageInfrastructure>(r => dummyStorageImplementation)
            .BuildServiceProvider();

        var messageDispatcherMock = Substitute.For<IServiceMessageDispatcher>();
        var manifests = new List<IServiceAssemblyManifest> { new DummyServiceManifest() };

        using var sut = new ServiceHost(serviceProvider, NullLogger<ServiceHost>.Instance, messageDispatcherMock, manifests);
        await sut.StartAsync(CancellationToken.None);

        // Assertions are in DummyService.Start... i know, this doesn't win a design price but i have no better idea at the moment.
    }

    public class DummyService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessagingInfrastructure _outerMessaging;
        private readonly IStorageInfrastructure _outerStorage;

        public DummyService(IServiceProvider serviceProvider, IMessagingInfrastructure outerMessaging, IStorageInfrastructure outerStorage)
        {
            _serviceProvider = serviceProvider;
            _outerMessaging = outerMessaging;
            _outerStorage = outerStorage;
        }

        public void Start() 
        {
            // inner and outer messaging should be the same... this is just redirected.
            Assert.Equal(_outerMessaging.GetHashCode(), DummyMessagingHashCode);
            Assert.Equal(_outerStorage.GetHashCode(), DummyStorageHashCode);

            using (var childScope = _serviceProvider.CreateScope())
            {
                var innerMessaging = childScope.ServiceProvider.GetRequiredService<IMessagingInfrastructure>();
                    
                // inner and outer messaging should be the same... this is just redirected.
                Assert.Equal(_outerMessaging.GetHashCode(), innerMessaging.GetHashCode());

            } // inner messaging was disposed here - because the redirection in ServiceHost.RedirectCommonServicesFromOuterContainer marked it as transient.

            Assert.False(((DummyMessaging)_outerMessaging).IsDisposed);
            Assert.False(((DummyStorage)_outerStorage).IsDisposed);
        }

        public void Kill() { }
        public void Stop() { }
    }

    public class DummyMessaging : IMessagingInfrastructure, IDisposable
    {
        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            IsDisposed = true;
        }

        public void Publish(Message message) { }
        public object Subscribe<TMessageHandler>() where TMessageHandler : IMessageHandler => Guid.NewGuid();
        public object Subscribe<TMessageHandler>(string routeFilterPattern) where TMessageHandler : IMessageHandler => Guid.NewGuid();
        public object Subscribe(Action<Message> handler) => Guid.NewGuid();
        public object Subscribe(string routeFilterPattern, Action<Message> handler) => Guid.NewGuid();
        public void Unsubscribe(object subscription) { }
    }

    public class DummyStorage : IStorageInfrastructure, IDisposable
    {
        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            IsDisposed = true;
        }

        public IStorageInfrastructure Set(string key, string value) => this;
        public IStorageInfrastructure Set(string area, string key, string value) => this;
        public IStorageInfrastructure Set(string key, byte[] value) => this;
        public IStorageInfrastructure Set(string area, string key, byte[] value) => this;

        public string GetString(string key) => string.Empty;
        public string GetString(string area, string key) => string.Empty;

        public byte[] GetBytes(string key) => Array.Empty<byte>();
        public byte[] GetBytes(string area, string key) => Array.Empty<byte>();

        public IStorageInfrastructure RemoveKey(string key) => this;
        public IStorageInfrastructure RemoveKey(string area, string key) => this;
        public IStorageInfrastructure RemoveArea(string area) => this;
    }

    public class DummyServiceManifest : IServiceAssemblyManifest
    {
        public string FriendlyName => nameof(DummyServiceManifest);

        public void RegisterDependencies(IServiceCollection services, IServiceHostContext context)
        {
            services.AddHosted<DummyService>();
        }
    }
}