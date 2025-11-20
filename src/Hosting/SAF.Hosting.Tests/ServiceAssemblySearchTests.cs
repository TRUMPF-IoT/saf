// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Tests;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SAF.Hosting.Contracts;
using TestUtilities;
using Xunit;

public class ServiceAssemblySearchTests
{
    [Fact]
    public void LoadServiceAssemblyManifestsReturnsServiceAssemblyManifest()
    {
        var loggerMock = Substitute.For<MockLogger<ServiceAssemblySearch>>();
        var optionsMock = Substitute.For<IOptions<ServiceAssemblySearchOptions>>();

        using var tempFile = new TemporaryFileCopy("SAF.Hosting.TestServices.dll");
        optionsMock.Value.Returns(new ServiceAssemblySearchOptions { SearchPath = tempFile.TempFileName });

        var search = new ServiceAssemblySearch(loggerMock, optionsMock);
        var manifests = search.LoadServiceAssemblyManifests();
        
        Assert.Single(manifests);
        loggerMock.AssertNotLogged(LogLevel.Error);
    }

    [Fact]
    public void LoadServiceAssemblyManifestsUsingPatternReturnsManifests()
    {
        var loggerMock = Substitute.For<MockLogger<ServiceAssemblySearch>>();
        var optionsMock = Substitute.For<IOptions<ServiceAssemblySearchOptions>>();

        const string targetPath = "TestData";
        using var tempFile1 = new TemporaryFileCopy("SAF.Hosting.TestServices.dll", targetPath);
        using var tempFile2 = new TemporaryFileCopy("SAF.Hosting.TestServices.dll", targetPath);

        optionsMock.Value.Returns(new ServiceAssemblySearchOptions
            { BasePath = Path.Combine(AppContext.BaseDirectory, targetPath), SearchPath = "*.dll" });

        var search = new ServiceAssemblySearch(loggerMock, optionsMock);
        var manifests = search.LoadServiceAssemblyManifests();

        Assert.Equal(2, manifests.Count());
        loggerMock.AssertNotLogged(LogLevel.Error);
    }

    [Fact]
    public void LoadServiceAssemblyManifestsWithSubDirectoriesReturnsManifests()
    {
        var loggerMock = Substitute.For<MockLogger<ServiceAssemblySearch>>();
        var optionsMock = Substitute.For<IOptions<ServiceAssemblySearchOptions>>();

        const string targetPath = "TestData";
        using var tempFile1 = new TemporaryFileCopy("SAF.Hosting.TestServices.dll", targetPath);
        using var tempFile2 = new TemporaryFileCopy("SAF.Hosting.TestServices.dll", Path.Combine(targetPath, "FilePatterns1"));
        using var tempFile3 = new TemporaryFileCopy("SAF.Hosting.TestServices.dll", Path.Combine(targetPath, "FilePatterns1"));
        using var tempFile4 = new TemporaryFileCopy("SAF.Hosting.TestServices.dll", Path.Combine(targetPath, "FilePatterns1", "SubDir"));
        using var tempFile5 = new TemporaryFileCopy("SAF.Hosting.TestServices.dll", Path.Combine(targetPath, "FilePatterns1", "SubDir"));

        optionsMock.Value.Returns(new ServiceAssemblySearchOptions
            { BasePath = Path.Combine(AppContext.BaseDirectory, targetPath), SearchPath = "**/*.dll" });

        var search = new ServiceAssemblySearch(loggerMock, optionsMock);
        var manifests = search.LoadServiceAssemblyManifests();

        Assert.Equal(5, manifests.Count());
        loggerMock.AssertNotLogged(LogLevel.Error);
    }

    [Fact]
    public void LoadServiceAssemblyManifestsLogsErrorForMissingManifest()
    {
        var loggerMock = Substitute.For<MockLogger<ServiceAssemblySearch>>();
        var optionsMock = Substitute.For<IOptions<ServiceAssemblySearchOptions>>();

        using var tempFile = new TemporaryFileCopy("SAF.Common.dll");
        optionsMock.Value.Returns(new ServiceAssemblySearchOptions { SearchPath = tempFile.TempFileName });

        var search = new ServiceAssemblySearch(loggerMock, optionsMock);
        var manifests = search.LoadServiceAssemblyManifests();
        Assert.Empty(manifests);

        loggerMock.AssertLogged(LogLevel.Error);
    }

    [Fact]
    public void LoadServiceAssemblyManifestsWithExclusionGlobDoesNotLogErrorsForExcludedAssemblies()
    {
        var loggerMock = Substitute.For<MockLogger<ServiceAssemblySearch>>();
        var optionsMock = Substitute.For<IOptions<ServiceAssemblySearchOptions>>();

        const string targetPath = "TestData";
        using var tempFile1 = new TemporaryFileCopy("SAF.Hosting.TestServices.dll", targetPath);
        using var tempFile2 = new TemporaryFileCopy("SAF.Common.dll", targetPath);
        using var tempFile3 = new TemporaryFileCopy("SAF.Hosting.TestServices.dll", Path.Combine(targetPath, "FilePatterns1"));
        using var tempFile4 = new TemporaryFileCopy("SAF.Common.dll", Path.Combine(targetPath, "FilePatterns1"));
        using var tempFile5 = new TemporaryFileCopy("SAF.Hosting.TestServices.dll", Path.Combine(targetPath, "FilePatterns1", "SubDir"));
        using var tempFile6 = new TemporaryFileCopy("SAF.Common.dll", Path.Combine(targetPath, "FilePatterns1", "SubDir"));

        optionsMock.Value.Returns(new ServiceAssemblySearchOptions
            { BasePath = Path.Combine(AppContext.BaseDirectory, targetPath), SearchPath = "**/SAF.Hosting.*.dll;|**/*Common*.dll" });

        var search = new ServiceAssemblySearch(loggerMock, optionsMock);
        var manifests = search.LoadServiceAssemblyManifests();

        Assert.Equal(3, manifests.Count());
        loggerMock.AssertNotLogged(LogLevel.Error);
    }

    [Fact]
    public void LoadServiceAssemblyManifestsWithWithFilterPatternDoesNotLogErrorsForFilteredAssemblies()
    {
        var loggerMock = Substitute.For<MockLogger<ServiceAssemblySearch>>();
        var optionsMock = Substitute.For<IOptions<ServiceAssemblySearchOptions>>();

        const string targetPath = "TestData";
        using var tempFile1 = new TemporaryFileCopy("SAF.Hosting.TestServices.dll", targetPath);
        using var tempFile2 = new TemporaryFileCopy("SAF.Common.dll", targetPath);

        optionsMock.Value.Returns(new ServiceAssemblySearchOptions
            { BasePath = Path.Combine(AppContext.BaseDirectory, targetPath), SearchPath = "*.dll", SearchFilenamePattern = "^((?!Common).)*$" });

        var search = new ServiceAssemblySearch(loggerMock, optionsMock);
        var manifests = search.LoadServiceAssemblyManifests();

        Assert.Single(manifests);
        loggerMock.AssertNotLogged(LogLevel.Error);
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(null, "**/*.txt", ".*")]
    [InlineData("FilePatterns1", null, ".*")]
    [InlineData("FilePatterns1", "**/*.txt", null)]
    public void LoadServiceAssemblyManifestsReturnsEmptySearchResultForInvalidOptions(string? basePath, string? searchPath, string? searchFilenamePattern)
    {
        var loggerMock = Substitute.For<MockLogger<ServiceAssemblySearch>>();
        var optionsMock = Substitute.For<IOptions<ServiceAssemblySearchOptions>>();

        optionsMock.Value.Returns(new ServiceAssemblySearchOptions { BasePath = basePath!, SearchPath = searchPath!, SearchFilenamePattern = searchFilenamePattern! });
        var search = new ServiceAssemblySearch(loggerMock, optionsMock);
        
        var manifests = search.LoadServiceAssemblyManifests();
        Assert.Empty(manifests);
        loggerMock.AssertLogged(LogLevel.Error);
    }
}