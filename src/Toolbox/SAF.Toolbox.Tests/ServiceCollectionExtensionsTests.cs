// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SAF.Common;
using SAF.Messaging.Contracts;
using Heartbeat;
using RequestClient;
using System.IO.Abstractions;
using Toolbox.FileTransfer;
using Xunit;

public class ServiceCollectionExtensionsTests
{
    private readonly ServiceCollection _services = [];

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
        _services.AddSingleton(_ => Substitute.For<IMessagingInfrastructure>());
        _services.AddRequestClient();

        using var provider = _services.BuildServiceProvider();
        Assert.NotNull(provider.GetServices<IRequestClient>());
        Assert.NotNull(provider.GetServices<IHeartbeatPool>());
    }

    [Fact]
    public void AddRequestClientAddsServiceAfterAddHeartbeatPoolOk()
    {
        _services.AddSingleton(_ => Substitute.For<IMessagingInfrastructure>());
        _services.AddHeartbeatPool();
        _services.AddRequestClient();

        using var provider = _services.BuildServiceProvider();
        Assert.NotNull(provider.GetServices<IRequestClient>());
        Assert.NotNull(provider.GetServices<IHeartbeatPool>());
    }

    [Fact]
    public void AddRequestClientAddsServiceOnlyOnceOk()
    {
        _services.AddSingleton(_ => Substitute.For<IMessagingInfrastructure>());
            
        _services.AddRequestClient();
        _services.AddRequestClient();
        _services.AddRequestClient();

        using var provider = _services.BuildServiceProvider();

        Assert.NotNull(provider.GetServices<IRequestClient>());
        Assert.Single(provider.GetServices<IRequestClient>());
        Assert.NotNull(provider.GetService<IRequestClient>());
    }
    
    [Fact]
    public void AddFileSenderWithoutConfigAddsServiceWithDefaultConfigOk()
    {
        // Arrange
        _services.AddSingleton(_ => Substitute.For<IMessagingInfrastructure>());
        _services.AddSingleton(_ => Substitute.For<ILogger<FileSender>>());
        
        // Act
        _services.AddFileSender();

        using var provider = _services.BuildServiceProvider();
        var fileSender = provider.GetRequiredService<IFileSender>();
        var options = provider.GetService<IOptions<FileSenderOptions>>();
        
        // Assert
        Assert.NotNull(fileSender);
        Assert.NotNull(options);
        Assert.NotNull(options.Value);
        Assert.Equal(0, options.Value.RetryAttemptsForFailedChunks);
        Assert.Equal(200 * 1024u, options.Value.MaxChunkSizeInBytes);
    }

    [Fact]
    public void AddFileSenderWithConfigAddsServiceWithSpecificConfigOk()
    {
        // Arrange
        _services.AddSingleton(_ => Substitute.For<IMessagingInfrastructure>());
        _services.AddSingleton(_ => Substitute.For<ILogger<FileSender>>());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            { "FileSender:RetryAttemptsForFailedChunks", "5" },
            { "FileSender:MaxChunkSizeInBytes", "1024" }
        }!).Build();
        
        // Act
        _services.AddFileSender(config);

        using var provider = _services.BuildServiceProvider();
        var fileSender = provider.GetService<IFileSender>();
        var options = provider.GetService<IOptions<FileSenderOptions>>();
        
        // Assert
        Assert.NotNull(fileSender);
        Assert.NotNull(options);
        Assert.NotNull(options.Value);
        Assert.Equal(5, options.Value.RetryAttemptsForFailedChunks);
        Assert.Equal(1024u, options.Value.MaxChunkSizeInBytes);
    }

    [Fact]
    public void AddFileReceiverWithoutConfigAddsServiceAndRequiredServicesOk()
    {
        // Arrange
        _services.AddSingleton(_ => Substitute.For<IMessagingInfrastructure>());
        _services.AddSingleton(_ => Substitute.For<ILoggerFactory>());
        _services.AddSingleton(_ => Substitute.For<ILogger<FileReceiver>>());

        // Act
        _services.AddFileReceiver();

        using var provider = _services.BuildServiceProvider();
        var options = provider.GetService<IOptions<FileReceiverOptions>>();
        var fileReceiver = provider.GetService<IFileReceiver>();
        var statefulFileReceiverFactory = provider.GetService<IStatefulFileReceiverFactory>();

        // Assert
        Assert.NotNull(options);
        Assert.NotNull(fileReceiver);
        Assert.NotNull(statefulFileReceiverFactory);
        Assert.Equal(72u, options.Value.StateExpirationAfterHours);
    }

    [Fact]
    public void AddFileReceiverWithConfigAddsServiceWithSpecificConfigOk()
    {
        // Arrange
        _services.AddSingleton(_ => Substitute.For<IMessagingInfrastructure>());
        _services.AddSingleton(_ => Substitute.For<ILoggerFactory>());
        _services.AddSingleton(_ => Substitute.For<ILogger<FileReceiver>>());
        
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            { "FileReceiver:StateExpirationAfterHours", "5" }
        }!).Build();

        // Act
        _services.AddFileReceiver(config);

        using var provider = _services.BuildServiceProvider();
        var options = provider.GetService<IOptions<FileReceiverOptions>>();
        var fileReceiver = provider.GetService<IFileReceiver>();
        var statefulFileReceiverFactory = provider.GetService<IStatefulFileReceiverFactory>();

        // Assert
        Assert.NotNull(options);
        Assert.NotNull(fileReceiver);
        Assert.NotNull(statefulFileReceiverFactory);
        Assert.Equal(5u, options.Value.StateExpirationAfterHours);
    }

    [Fact]
    public void AddFileHandlingCreatesFallbackDirectoryWhenNoHostInfoIsAvailable()
    {
        _services.AddFileHandling();

        using var provider = _services.BuildServiceProvider();

        var directoryInfo = provider.GetRequiredService<IDirectoryInfo>();

        Assert.True(directoryInfo.Exists);
        Assert.EndsWith("tempfs", directoryInfo.FullName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddFileHandlingUsesHostInfoBasePathWhenAvailable()
    {
        var userBasePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var hostInfo = Substitute.For<IServiceHostInfo>();
        hostInfo.FileSystemUserBasePath.Returns(userBasePath);

        _services.AddSingleton(hostInfo);
        _services.AddFileHandling();

        using var provider = _services.BuildServiceProvider();

        var directoryInfo = provider.GetRequiredService<IDirectoryInfo>();

        Assert.True(directoryInfo.Exists);
        Assert.Equal(userBasePath, directoryInfo.FullName);
    }
}


