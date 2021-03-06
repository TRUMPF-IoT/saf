// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using SAF.Common;

namespace SAF.Messaging.Redis
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRedisMessagingInfrastructure(this IServiceCollection serviceCollection, Action<RedisConfiguration> configure, Action<Message> traceAction = null)
        {
            var config = new RedisConfiguration();
            configure?.Invoke(config);

            return serviceCollection.AddRedisMessagingInfrastructure(config, traceAction);
        }

        public static IServiceCollection AddRedisStorageInfrastructure(this IServiceCollection serviceCollection, Action<RedisConfiguration> configure)
        {
            var config = new RedisConfiguration();
            configure(config);

            return serviceCollection.AddRedisStorageInfrastructure(config);
        }

        public static IServiceCollection AddRedisInfrastructure(this IServiceCollection serviceCollection, Action<RedisConfiguration> configure, Action<Message> traceAction = null)
        {
            var config = new RedisConfiguration();
            configure?.Invoke(config);

            return serviceCollection.AddRedisMessagingInfrastructure(config, traceAction)
                .AddRedisStorageInfrastructure(config)
                .AddSingleton<IMessagingInfrastructure>(sp => sp.GetRequiredService<IRedisMessagingInfrastructure>());
        }

        internal static IServiceCollection AddRedisMessagingInfrastructure(this IServiceCollection serviceCollection, MessagingConfiguration config)
        {
            serviceCollection
                .AddTransient(sp => new Func<MessagingConfiguration, IRedisMessagingInfrastructure>(cfg =>
                {
                    var msgCfg = new RedisMessagingConfiguration(cfg);
                    var redisCfg = new RedisConfiguration { ConnectionString = msgCfg.ConnectionString };
                    return new Messaging(sp.GetRequiredService<ILogger<Messaging>>(),
                        CreateRedisConnection(redisCfg),
                        sp.GetRequiredService<IServiceMessageDispatcher>(),
                        null);
                }))
                .AddTransient(sp => sp.GetRequiredService<Func<MessagingConfiguration, IRedisMessagingInfrastructure>>().Invoke(config));

            return serviceCollection;
        }

        private static IConnectionMultiplexer CreateRedisConnection(RedisConfiguration config)
        {
            var options = ConfigurationOptions.Parse(config.ConnectionString);

            // auto reconnect
            options.AbortOnConnectFail = false;

            // #227 - try to increase timeouts
            options.ConnectTimeout = 20000;
            options.SyncTimeout = 5000;
            options.ConnectRetry = 10;
            options.ClientName = "SAF-PubSub";

            ThreadPool.GetMinThreads(out var minWorker, out var minCompletionPort);
            ThreadPool.GetMaxThreads(out var maxWorker, out var maxCompletionPort);
            ThreadPool.SetMinThreads(Math.Min(maxWorker, Math.Max(50, minWorker)), Math.Min(maxCompletionPort, Math.Max(50, minCompletionPort)));

            return ConnectionMultiplexer.Connect(options);
        }

        private static IServiceCollection AddRedisMessagingInfrastructure(this IServiceCollection serviceCollection, RedisConfiguration config, Action<Message> traceAction)
        {
            return serviceCollection.AddTransient<IRedisMessagingInfrastructure>(r =>
                new Messaging(r.GetRequiredService<ILogger<Messaging>>(),
                    CreateRedisConnection(config),
                    r.GetRequiredService<IServiceMessageDispatcher>(),
                    traceAction));
        }

        private static IServiceCollection AddRedisStorageInfrastructure(this IServiceCollection serviceCollection, RedisConfiguration config)
        {
            return serviceCollection.AddTransient<IStorageInfrastructure>(r =>
                new Storage(CreateRedisConnection(config)));
        }
    }
}