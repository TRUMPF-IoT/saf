// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.IO.Abstractions;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SAF.Toolbox.FileTransfer;
using SAF.Toolbox.Heartbeat;
using SAF.Toolbox.RequestClient;

[assembly: InternalsVisibleTo("SAF.Toolbox.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace SAF.Toolbox
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHeartbeat(this IServiceCollection services, int heartbeatMillis = 1000)
            => services.AddSingleton<IHeartbeat, Heartbeat.Heartbeat>(ctx => new Heartbeat.Heartbeat(heartbeatMillis));

        public static IServiceCollection AddHeartbeatPool(this IServiceCollection services)
            => services.AddSingleton<IHeartbeatPool, HeartbeatPool>()
                .AddTransient<Func<int, IHeartbeat>>(sp => heartbeatMillis =>
                {
                    var factory = sp.GetRequiredService<IHeartbeatPool>();
                    return factory.GetOrCreateHeartbeat(heartbeatMillis);
                });

        public static IServiceCollection AddFileHandling(this IServiceCollection services)
            => services.AddTransient<IFileSystem, FileSystem>();

        public static IServiceCollection AddRequestClient(this IServiceCollection services)
            => services.AddSingleton<IRequestClient, RequestClient.RequestClient>();

        public static IServiceCollection AddFileSender(this IServiceCollection services)
        {
            services.AddTransient<IFileSender, FileSender>();
            return services;
        }

        public static IServiceCollection AddFileReceiver(this IServiceCollection services)
        {
            services.AddTransient<IFileReceiver, FileReceiver>();
            services.AddTransient<IStatefulFileReceiver, StatefulFileReceiver>();
            return services;
        }

        public static IServiceCollection AddServiceConfiguration<TServiceConfiguration>(this IServiceCollection services, IConfiguration hostConfig, string configName)
            where TServiceConfiguration : class, new()
        {
            services.Configure<TServiceConfiguration>(hostConfig.GetSection(configName))
                .AddSingleton(sp => sp.GetService<IOptions<TServiceConfiguration>>().Value);

            return services;
        }
    }
}