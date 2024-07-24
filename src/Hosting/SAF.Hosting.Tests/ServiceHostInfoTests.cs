// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Tests;

using Contracts;
using NSubstitute;
using Xunit;

public class ServiceHostInfoTests
{
    [Fact]
    public void IdReturnsInitializedIdWhenIdInOptionsIsNotNull()
    {
        // Arrange
        var options = new ServiceHostInfoOptions { Id = "test-id" };
        var initializeId = Substitute.For<Func<string>>();
        var serviceHostInfo = new ServiceHostInfo(options, initializeId);

        // Act
        var id = serviceHostInfo.Id;

        // Assert
        Assert.Equal("test-id", id);
        initializeId.DidNotReceive().Invoke();
    }

    [Fact]
    public void IdInvokesInitializeIdWhenIdInOptionsIsNull()
    {
        // Arrange
        var options = new ServiceHostInfoOptions { Id = null };
        var initializeId = Substitute.For<Func<string>>();
        initializeId.Invoke().Returns("initialized-id");
        var serviceHostInfo = new ServiceHostInfo(options, initializeId);

        // Act
        var id = serviceHostInfo.Id;

        // Assert
        Assert.Equal("initialized-id", id);
        initializeId.Received(1).Invoke();
    }

    [Fact]
    public void ServiceHostTypeReturnsServiceHostTypeFromOptions()
    {
        // Arrange
        var options = new ServiceHostInfoOptions { ServiceHostType = "test-type" };
        var initializeId = Substitute.For<Func<string>>();
        var serviceHostInfo = new ServiceHostInfo(options, initializeId);

        // Act
        var serviceHostType = serviceHostInfo.ServiceHostType;

        // Assert
        Assert.Equal("test-type", serviceHostType);
    }

    [Fact]
    public void FileSystemUserBasePathReturnsFileSystemUserBasePathFromOptions()
    {
        // Arrange
        var options = new ServiceHostInfoOptions { FileSystemUserBasePath = "user-base-path" };
        var initializeId = Substitute.For<Func<string>>();
        var serviceHostInfo = new ServiceHostInfo(options, initializeId);

        // Act
        var fileSystemUserBasePath = serviceHostInfo.FileSystemUserBasePath;

        // Assert
        Assert.Equal("user-base-path", fileSystemUserBasePath);
    }

    [Fact]
    public void FileSystemInstallationPathReturnsFileSystemInstallationPathFromOptions()
    {
        // Arrange
        var options = new ServiceHostInfoOptions { FileSystemInstallationPath = "installation-path" };
        var initializeId = Substitute.For<Func<string>>();
        var serviceHostInfo = new ServiceHostInfo(options, initializeId);

        // Act
        var fileSystemInstallationPath = serviceHostInfo.FileSystemInstallationPath;

        // Assert
        Assert.Equal("installation-path", fileSystemInstallationPath);
    }

    [Fact]
    public void UpSinceReturnsCurrentDateTimeOffset()
    {
        // Arrange
        var options = new ServiceHostInfoOptions();
        var initializeId = Substitute.For<Func<string>>();
        var serviceHostInfo = new ServiceHostInfo(options, initializeId);

        // Act
        var upSince = serviceHostInfo.UpSince;

        // Assert
        Assert.True((DateTimeOffset.Now - upSince).TotalSeconds < 1);
    }
}