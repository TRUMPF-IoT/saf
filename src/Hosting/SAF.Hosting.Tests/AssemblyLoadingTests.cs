// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TestUtilities;
using Xunit;

namespace SAF.Hosting.Tests
{
    public class AssemblyLoadingTests
    {
        [Fact]
        public void DllWithManifestThrowsNoErrorOnLoading()
        {
            // Arrange
            var loggerMock = Substitute.For<MockLogger>();
            var servicesMock = Substitute.For<IServiceCollection>();

            using var tempFile = new TemporaryFileCopy("SAF.Hosting.TestServices.dll");

            var t = tempFile;
            // Act
            servicesMock.AddHost(settings => settings.SearchPath = $"./{t.TempFileName}", loggerMock);

            // Assert

            // ... added one manifest
            servicesMock.Received(1).Add(Arg.Is<ServiceDescriptor>(sd => sd.ServiceType.Name == "IServiceAssemblyManifest"));

            // ... added the service host (as self-implementation) and the service message dispatcher (as IServiceMessageDispatcher)
            servicesMock.Received(1).Add(Arg.Is<ServiceDescriptor>(sd => sd.ServiceType.Name == "ServiceHost" && sd.ImplementationType != null && sd.ImplementationType.Name == "ServiceHost"));
            servicesMock.Received(1).Add(Arg.Is<ServiceDescriptor>(sd => sd.ServiceType.Name == "IServiceMessageDispatcher" && sd.ImplementationType != null && sd.ImplementationType.Name == "ServiceMessageDispatcher"));

            // ... and no error logged
            loggerMock.AssertNotLogged(LogLevel.Error);
        }

        [Fact]
        public void DllWithoutManifestLogsAnErrorOnLoading()
        {
            // Arrange
            var loggerMock = Substitute.For<MockLogger<TemporaryFileCopy>>();
            var serviceCollectionMock = Substitute.For<IServiceCollection>();

            using (var tempFile = new TemporaryFileCopy("SAF.Common.dll"))
            {
                var t = tempFile;
                // Act
                serviceCollectionMock.AddHost(settings => settings.SearchPath = $"./{t.TempFileName}", loggerMock);

                // Assert

                // ... added NO manifest (no valid dll loaded)
                serviceCollectionMock.DidNotReceive().Add(Arg.Is<ServiceDescriptor>(sd => sd.ServiceType.Name == "IServiceAssemblyManifest"));

                // ... added the service host (as self-implementation) and the service message dispatcher (as IServiceMessageDispatcher)
                serviceCollectionMock.Received(1).Add(Arg.Is<ServiceDescriptor>(sd => sd.ServiceType.Name == "ServiceHost" && sd.ImplementationType != null && sd.ImplementationType.Name == "ServiceHost"));
                serviceCollectionMock.Received(1).Add(Arg.Is<ServiceDescriptor>(sd => sd.ServiceType.Name == "IServiceMessageDispatcher" && sd.ImplementationType != null && sd.ImplementationType.Name == "ServiceMessageDispatcher"));

                // ... and the error is logged
                loggerMock.AssertLogged(LogLevel.Error);
            }
        }
    }
}