// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SAF.Common;
using Xunit;

namespace SAF.Messaging.Routing.Tests;

public class AssemblyLoadingTests
{
    private string TestAssemblyPath => Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().Location).LocalPath)!;
    private string TestDataPath => Path.Combine(TestAssemblyPath, "TestData");

    private RoutingConfiguration[] TestRoutings => new[]
    {
        new RoutingConfiguration
        {
            Messaging = new MessagingConfiguration {Type = "IRedisMessagingInfrastructure"}
        }
    };

    [Fact]
    public void AddsSafDefaultMessagingAssembliesToServiceCollectionWorks()
    {
        var servicesMock = Substitute.For<IServiceCollection>();
        servicesMock.AddRoutingMessagingInfrastructure(config => config.Routings = TestRoutings);

        servicesMock.Received(1).Add(Arg.Is<ServiceDescriptor>(sd => sd.ServiceType.Name == "IRedisMessagingInfrastructure"));
        servicesMock.Received(1).Add(Arg.Is<ServiceDescriptor>(sd => sd.ServiceType.Name == "IRoutingMessagingInfrastructure"));
    }

    [Fact]
    public void AddsSafMessagingAssembliesPerPatternToServiceCollectionWorks()
    {
        var servicesMock = Substitute.For<IServiceCollection>();
        servicesMock.AddRoutingMessagingInfrastructure(config =>
        {
            config.SearchPath = "./SAF.Messaging.Redis.dll";
            config.Routings = TestRoutings;
        });

        servicesMock.Received(1).Add(Arg.Is<ServiceDescriptor>(sd => sd.ServiceType.Name == "IRedisMessagingInfrastructure"));
        servicesMock.Received(1).Add(Arg.Is<ServiceDescriptor>(sd => sd.ServiceType.Name == "IRoutingMessagingInfrastructure"));
    }

    [Fact]
    public void AddsSafMessagingAssembliesFromBasePathToServiceCollectionWorks()
    {
        var servicesMock = Substitute.For<IServiceCollection>();
        servicesMock.AddRoutingMessagingInfrastructure(config =>
        {
            config.BasePath = TestAssemblyPath;
            config.SearchPath = "./SAF.Messaging.Redis.dll";
            config.Routings = TestRoutings;
        });

        servicesMock.Received(1).Add(Arg.Is<ServiceDescriptor>(sd => sd.ServiceType.Name == "IRedisMessagingInfrastructure"));
        servicesMock.Received(1).Add(Arg.Is<ServiceDescriptor>(sd => sd.ServiceType.Name == "IRoutingMessagingInfrastructure"));
    }

    [Fact]
    public void SearchingServiceAssembliesWithSubDirectoryWorks()
    {
        var result = ServiceCollectionExtensions.SearchMessagingAssemblies(System.IO.Path.Combine(TestDataPath, "FilePatterns1"), "**/*.txt", ".*").ToList();
        // All should match -> just compare count
        Assert.Equal(6, result.Count);
    }

    [Fact]
    public void SearchingMessagingAssembliesWithoutSubDirectoryWorksCorrectly()
    {
        var result = ServiceCollectionExtensions.SearchMessagingAssemblies(Path.Combine(TestDataPath, "FilePatterns1"), "*.txt", ".*").ToList();
            
        Assert.Equal(2, result.Count);
        Assert.Contains(Path.Combine(TestDataPath, "FilePatterns1", "My.Messaging.3.txt"), result);
        Assert.Contains(Path.Combine(TestDataPath, "FilePatterns1", "My.Messaging.3.Contracts.txt"), result);
    }

    [Fact]
    public void SearchingMessagingAssembliesWithExclusionGlobWorksCorrectly()
    {
        var result = ServiceCollectionExtensions.SearchMessagingAssemblies(Path.Combine(TestDataPath, "FilePatterns1"), "**/My.Messaging*.txt;|**/*Contracts*.txt", ".*").ToList();

        Assert.Equal(3, result.Count);
        Assert.Contains(Path.Combine(TestDataPath, "FilePatterns1", "My.Messaging.3.txt"), result);
        Assert.Contains(Path.Combine(TestDataPath, "FilePatterns1", "SubDir", "My.Messaging.1.txt"), result);
        Assert.Contains(Path.Combine(TestDataPath, "FilePatterns1", "SubDir", "My.Messaging.2.txt"), result);
    }

    [Fact]
    public void SearchingMessagingAssembliesWithFilterPatternWorksCorrectly()
    {
        var result = ServiceCollectionExtensions.SearchMessagingAssemblies(Path.Combine(TestDataPath, "FilePatterns1"), "*.txt", "^((?!Contracts).)*$");

        Assert.Single(result);
        Assert.Contains(Path.Combine(TestDataPath, "FilePatterns1", "My.Messaging.3.txt"), result);
    }
}