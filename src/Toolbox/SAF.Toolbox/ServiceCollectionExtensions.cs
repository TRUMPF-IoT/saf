// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using System.IO.Abstractions;
using Testably.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SAF.Common;
using SAF.Messaging.Contracts;
using SAF.Toolbox.Heartbeat;
using SAF.Toolbox.RequestClient;

namespace SAF.Toolbox;

using FileTransfer;
using FileTransfer.Chunks;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHeartbeat(this IServiceCollection services, int heartbeatMillis = 1000)
        => services.AddSingleton<IHeartbeat>(_ => new Heartbeat.Heartbeat(heartbeatMillis));

    public static IServiceCollection AddHeartbeatPool(this IServiceCollection services)
    {
        services.TryAddSingleton<IHeartbeatPool, HeartbeatPool>();
        services.TryAddTransient<Func<int, IHeartbeat>>(sp => heartbeatMillis =>
        {
            var factory = sp.GetRequiredService<IHeartbeatPool>();
            return factory.GetOrCreateHeartbeat(heartbeatMillis);
        });
            
        return services;
    }

    public static IServiceCollection AddFileHandling(this IServiceCollection services)
    {
        services.TryAddTransient<IFileSystem, RealFileSystem>();
        services.TryAddTransient<IDirectoryInfo>(sp =>
        {
            var hostInfo = sp.GetService<IServiceHostInfo>();
            var fs = sp.GetRequiredService<IFileSystem>();
            var basePath = hostInfo?.FileSystemUserBasePath ?? Path.Combine(AppContext.BaseDirectory, "tempfs");
            var di = fs.DirectoryInfo.New(basePath);

            if (!di.Exists)
            {
                di.Create();
            }

            return di;
        });

        return services;
    }

    public static IServiceCollection AddRequestClient(this IServiceCollection services)
    {
        services.AddHeartbeatPool();
            
        services.TryAddSingleton<IRequestClient>(sp =>
        {
            var pool = sp.GetRequiredService<IHeartbeatPool>();
            return new RequestClient.RequestClient(
                sp.GetRequiredService<IMessagingInfrastructure>(),
                pool.GetOrCreateHeartbeat(1000),
                sp.GetService<ILogger<RequestClient.RequestClient>>());
        });

        return services;
    }
    
    public static IServiceCollection AddFileSender(this IServiceCollection services)
        => services.AddFileSender(_ => { });

    public static IServiceCollection AddFileSender(this IServiceCollection services, IConfiguration hostConfig)
        => services.AddFileSender(options => { hostConfig.GetSection(nameof(FileSender)).Bind(options); });

    public static IServiceCollection AddFileSender(this IServiceCollection services, Action<FileSenderOptions> configure)
    {
        services.TryAddTransient<IFileSystem, RealFileSystem>();
        services.AddRequestClient();

        services.Configure(configure);
        services.TryAddTransient<IFileSender>(sp => CreateFileSender(
            sp.GetRequiredService<ILogger<FileSender>>(),
            sp.GetRequiredService<IOptions<FileSenderOptions>>(),
            sp.GetRequiredService<IFileSystem>(),
            sp.GetRequiredService<IRequestClient>(),
            sp.GetRequiredService<IMessagingInfrastructure>()));
        return services;
    }

    internal static FileSender CreateFileSender(ILogger<FileSender> log, IOptions<FileSenderOptions> options, IFileSystem fileSystem,
        IRequestClient requestClient, IMessagingInfrastructure messaging)
    {
        IFileChunkSender WithRetries(IFileChunkSender sender) => new RetryingFileChunkSender(sender,
            options.Value.RetryAttemptsForFailedChunks, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)), log);

        // In order of preference.
        return new FileSender(log, options, fileSystem, requestClient, messaging,
            [WithRetries(new BinaryFileChunkSender(requestClient)), WithRetries(new TextFileChunkSender(requestClient))]);
    }

    public static IServiceCollection AddFileReceiver(this IServiceCollection services)
        => services.AddFileReceiver(_ => { });

    public static IServiceCollection AddFileReceiver(this IServiceCollection services, IConfiguration hostConfig)
        => services.AddFileReceiver(options => { hostConfig.GetSection(nameof(FileReceiver)).Bind(options); });

    public static IServiceCollection AddFileReceiver(this IServiceCollection services, Action<FileReceiverOptions> configure)
    {
        services.Configure(configure);

        services.TryAddTransient<IFileReceiver>(sp => CreateFileReceiver(
            sp.GetRequiredService<ILogger<FileReceiver>>(),
            sp.GetRequiredService<IMessagingInfrastructure>()));

        services.TryAddTransient<IFileSystem, RealFileSystem>();
        services.TryAddTransient<IStatefulFileReceiverFactory, StatefulFileReceiverFactory>();

        services.AddHeartbeatPool();
        return services;
    }

    internal static FileReceiver CreateFileReceiver(ILogger<FileReceiver> log, IMessagingInfrastructure messaging)
        => new(log, messaging, [new BinaryFileChunkReader(), new TextFileChunkReader()]);

    public static IServiceCollection AddServiceConfiguration<TServiceConfiguration>(this IServiceCollection services, IConfiguration hostConfig, string configName)
        where TServiceConfiguration : class, new()
    {
        services.Configure<TServiceConfiguration>(hostConfig.GetSection(configName))
            .AddSingleton(sp => sp.GetRequiredService<IOptions<TServiceConfiguration>>().Value);

        return services;
    }
}

