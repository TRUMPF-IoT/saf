﻿// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Tests;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Abstractions;
using Xunit;

public class ServiceAssemblyManagerTests
{
    [Fact]
    public void GetServiceAssemblyManifestsReturnsServiceAssemblies()
    {
        var serviceAssemblies = new List<IServiceAssemblyManifest>
        {
            Substitute.For<IServiceAssemblyManifest>(),
            Substitute.For<IServiceAssemblyManifest>()
        };

        var serviceAssemblyManager = new ServiceAssemblyManager(
            Substitute.For<ILogger<ServiceAssemblyManager>>(),
            null,
            serviceAssemblies);

        var manifests = serviceAssemblyManager.GetServiceAssemblyManifests();

        Assert.Equal(serviceAssemblies, manifests);
    }

    [Fact]
    public void GetServiceAssemblyManifestsReturnsServiceAssembliesFromAssemblySearch()
    {
        var serviceAssemblies = new List<IServiceAssemblyManifest>
        {
            Substitute.For<IServiceAssemblyManifest>(),
            Substitute.For<IServiceAssemblyManifest>()
        };

        var assemblySearch = Substitute.For<IServiceAssemblySearch>();
        assemblySearch.LoadServiceAssemblyManifests().Returns(serviceAssemblies);

        var serviceAssemblyManager = new ServiceAssemblyManager(
            Substitute.For<ILogger<ServiceAssemblyManager>>(),
            assemblySearch,
            new List<IServiceAssemblyManifest>());

        var manifests = serviceAssemblyManager.GetServiceAssemblyManifests();

        Assert.Equal(serviceAssemblies, manifests);
    }

    [Fact]
    public void GetServiceAssemblyManifestsReturnsAllServiceAssemblies()
    {
        var serviceAssemblies = new List<IServiceAssemblyManifest>
        {
            Substitute.For<IServiceAssemblyManifest>(),
            Substitute.For<IServiceAssemblyManifest>()
        };

        var searchServiceAssemblies = new List<IServiceAssemblyManifest>
        {
            Substitute.For<IServiceAssemblyManifest>(),
            Substitute.For<IServiceAssemblyManifest>()
        };

        var assemblySearch = Substitute.For<IServiceAssemblySearch>();
        assemblySearch.LoadServiceAssemblyManifests().Returns(searchServiceAssemblies);

        var serviceAssemblyManager = new ServiceAssemblyManager(
            Substitute.For<ILogger<ServiceAssemblyManager>>(),
            assemblySearch,
            serviceAssemblies);

        var manifests = serviceAssemblyManager.GetServiceAssemblyManifests();

        Assert.Equal(4, manifests.Count());
    }
}