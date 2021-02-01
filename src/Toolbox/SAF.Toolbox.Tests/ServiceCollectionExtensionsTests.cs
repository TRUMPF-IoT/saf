using System;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SAF.Common;
using SAF.Toolbox.Heartbeat;
using SAF.Toolbox.RequestClient;
using Xunit;

namespace SAF.Toolbox.Tests
{
    public class ServiceCollectionExtensionsTests
    {
        private readonly ServiceCollection _services;

        public ServiceCollectionExtensionsTests()
        {
            _services = new ServiceCollection();
        }

        [Fact]
        public void AddHeartbeatPoolAddsServiceOk()
        {
            _services.AddHeartbeatPool();
            
            using var provider = _services.BuildServiceProvider();
            Assert.NotNull(provider.GetService<IHeartbeatPool>());
            Assert.NotNull(provider.GetService<Func<int, IHeartbeat>>());
        }

        [Fact]
        public void AddHeartbeatPoolAddsServiceOnlyOnceOk()
        {
            _services.AddHeartbeatPool();
            _services.AddHeartbeatPool();
            _services.AddHeartbeatPool();

            using var provider = _services.BuildServiceProvider();
            
            Assert.NotNull(provider.GetServices<IHeartbeatPool>());
            Assert.Single(provider.GetServices<IHeartbeatPool>());
            Assert.NotNull(provider.GetService<IHeartbeatPool>());
            Assert.NotNull(provider.GetService<Func<int, IHeartbeat>>());
        }

        [Fact]
        public void AddRequestClientAddsServiceAndRequiredServicesOk()
        {
            _services.AddSingleton(sp => Substitute.For<IMessagingInfrastructure>());
            _services.AddRequestClient();

            using var provider = _services.BuildServiceProvider();
            Assert.NotNull(provider.GetServices<IRequestClient>());
            Assert.NotNull(provider.GetServices<IHeartbeatPool>());
        }

        [Fact]
        public void AddRequestClientAddsServiceAfterAddHeartbeatPoolOk()
        {
            _services.AddSingleton(sp => Substitute.For<IMessagingInfrastructure>());
            _services.AddHeartbeatPool();
            _services.AddRequestClient();

            using var provider = _services.BuildServiceProvider();
            Assert.NotNull(provider.GetServices<IRequestClient>());
            Assert.NotNull(provider.GetServices<IHeartbeatPool>());
        }

        [Fact]
        public void AddRequestClientAddsServiceOnlyOnceOk()
        {
            _services.AddSingleton(sp => Substitute.For<IMessagingInfrastructure>());
            
            _services.AddRequestClient();
            _services.AddRequestClient();
            _services.AddRequestClient();

            using var provider = _services.BuildServiceProvider();

            Assert.NotNull(provider.GetServices<IRequestClient>());
            Assert.Single(provider.GetServices<IRequestClient>());
            Assert.NotNull(provider.GetService<IRequestClient>());
        }
    }
}