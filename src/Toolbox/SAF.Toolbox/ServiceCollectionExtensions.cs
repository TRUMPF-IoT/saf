// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.IO.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SAF.Common;
using SAF.Toolbox.Heartbeat;
using SAF.Toolbox.FileTransfer;
using SAF.Toolbox.RequestClient;

namespace SAF.Toolbox;

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
        services.TryAddTransient<IFileSystem, FileSystem>();
        services.TryAddTransient(sp =>
            {
                var hi = sp.GetRequiredService<IHostInfo>();
                var fs = sp.GetRequiredService<IFileSystem>();
                var di = fs.DirectoryInfo.New(hi.FileSystemUserBasePath);
                if (!di.Exists) di.Create();
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
    {
        services.AddFileSender(null);
        return services;
    }

    public static IServiceCollection AddFileSender(this IServiceCollection services, IConfiguration? hostConfig)
    {
        services.TryAddTransient<IFileSystem, FileSystem>();
        services.AddRequestClient();

        services.TryAddTransient<IFileSender, FileSender>();

        if (hostConfig == null)
        {
            // Assure default configuration
            services.Configure<FileSenderOptions>(_ => { });
            return services;
        }
        services.AddServiceConfiguration<FileSenderOptions>(hostConfig, nameof(FileSender));

        return services;
    }

    public static IServiceCollection AddFileReceiver(this IServiceCollection services)
    {
        services.TryAddTransient<IFileReceiver, FileReceiver>();

        services.TryAddTransient<IFileSystem, FileSystem>();
        services.TryAddTransient<IStatefulFileReceiver, StatefulFileReceiver>();
        return services;
    }

    public static IServiceCollection AddServiceConfiguration<TServiceConfiguration>(this IServiceCollection services, IConfiguration hostConfig, string configName)
        where TServiceConfiguration : class, new()
    {
        services.Configure<TServiceConfiguration>(hostConfig.GetSection(configName))
            .AddSingleton(sp => sp.GetRequiredService<IOptions<TServiceConfiguration>>().Value);

        return services;
    }
}