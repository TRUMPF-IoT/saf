// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SAF.Common;
using Xunit;

namespace SAF.Hosting.Tests
{
    public class ServiceHostTests
    {
        private string TestAssemblyPath => Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().Location).LocalPath);

        private string TestDataPath => System.IO.Path.Combine(TestAssemblyPath, "TestData");

        [Fact]
        public void StartsLoadedServices()
        {
            // Arrange
            var callCounters = new CallCounters();
            using(var sut = SetupServiceHostWithCallCountersService(callCounters))
            {
                // Act
                sut.StartServices();

                // Assert
                Assert.Equal(1, callCounters.StartCalled);
                Assert.Equal(0, callCounters.StopCalled);
                Assert.Equal(0, callCounters.KillCalled);
            }
        }

        [Fact]
        public void StopsLoadedServicesOnDispose()
        {
            // Arrange
            var callCounters = new CallCounters();
            using(var sut = SetupServiceHostWithCallCountersService(callCounters))
            {
                // Act
                sut.StartServices();
            }

            // Assert
            Assert.Equal(1, callCounters.StartCalled);
            Assert.Equal(1, callCounters.StopCalled);
            Assert.Equal(0, callCounters.KillCalled);
        }

        [Fact]
        public void RegistersHandlersWithinDispatchers()
        {
            // Arrange
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var serviceAssemblies = new List<IServiceAssemblyManifest> { new CountingTestAssemblyManifest(null, true) };
            var dispatcher = new MessageDispatcher(null);

            // Act
            using(new ServiceHost(serviceProvider, null, dispatcher, serviceAssemblies))
            {
                // Assert
                Assert.Single(dispatcher.RegisteredHandlers);
                Assert.Equal("SAF.Hosting.Tests.ServiceHostTests+CountingTestHandler", dispatcher.RegisteredHandlers.First());
            }
        }

        [Fact]
        public void DispatcherCallsCorrectHandler()
        {
            // Arrange
            var callCounters = new CallCounters();
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var serviceAssemblies = new List<IServiceAssemblyManifest> { new CountingTestAssemblyManifest(callCounters, true) };
            var dispatcher = new MessageDispatcher(null);

            using(new ServiceHost(serviceProvider, null, dispatcher, serviceAssemblies))
            {
                // Act
                dispatcher.DispatchMessage("SAF.Hosting.Tests.ServiceHostTests+CountingTestHandler", new Message());

                // Assert
                Assert.Equal(1, callCounters.CanHandleCalled);
                Assert.Equal(1, callCounters.HandleCalled);
            }
        }

        [Fact]
        public void SearchingServiceAssembliesWithWrongParameters()
        {
            Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.SearchServiceAssemblies(null, "**/*.txt", ".*"));
            Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.SearchServiceAssemblies(System.IO.Path.Combine(TestDataPath, "FilePatterns1"), null, ".*"));
            Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.SearchServiceAssemblies(System.IO.Path.Combine(TestDataPath, "FilePatterns1"), "**/*.txt", null));
        }

        [Fact]
        public void SearchingServiceAssembliesWithSubdirectoryWorks()
        {
            var result = ServiceCollectionExtensions.SearchServiceAssemblies(System.IO.Path.Combine(TestDataPath, "FilePatterns1"), "**/*.txt", ".*").ToList();
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
        
        private static ServiceHost SetupServiceHostWithCallCountersService(CallCounters callCounters)
        {
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var serviceAssemblies = new List<IServiceAssemblyManifest> { new CountingTestAssemblyManifest(callCounters) };
            return new ServiceHost(serviceProvider, null, null, serviceAssemblies);
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
            private readonly bool _registerAHandler;

            public CountingTestAssemblyManifest(CallCounters counters, bool registerAHandler = false)
            {
                _counters = counters;
                _registerAHandler = registerAHandler;
            }

            public string FriendlyName => "Internal test service - only for this test";

            public void RegisterDependencies(IServiceCollection services, IServiceHostContext context)
            {
                services.AddHosted<CountingTestService>();
                services.AddSingleton(typeof(CallCounters), r => _counters);

                if(_registerAHandler)
                    services.AddTransient<CountingTestHandler>();
            }
        }

        internal class CountingTestService : IHostedService
        {
            private readonly CallCounters _counters;
            public CountingTestService(CallCounters counters) => _counters = counters ?? new CallCounters();
            public void Start() => _counters.StartCalled++;
            public void Stop() => _counters.StopCalled++;
            public void Kill() => _counters.KillCalled++;
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
}