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
                        CreateRedisConnectionMultipleTimes(redisCfg, sp.GetRequiredService<ILogger<Messaging>>()),
                        sp.GetRequiredService<IServiceMessageDispatcher>(),
                        null);
                }))
                .AddTransient(sp => sp.GetRequiredService<Func<MessagingConfiguration, IRedisMessagingInfrastructure>>().Invoke(config));

            return serviceCollection;
        }

        /// <summary>
        /// Try to connect several times and throw an error message if it fails.
        /// </summary>
        private static  IConnectionMultiplexer CreateRedisConnectionMultipleTimes(RedisConfiguration config, ILogger logger)
        {
            IConnectionMultiplexer con = CreateRedisConnection(config);
            int testCount = 10;
            while (!con.IsConnected)
            {
                testCount--;
                logger.LogInformation($"Not yet connected, remaining tries: {testCount}");
                if (testCount == 0) throw new ApplicationException("Redis is not available");
                System.Threading.Thread.Sleep(500);
                con = CreateRedisConnection(config);
            }
            logger.LogInformation("Successfully connected to redis");
            return con;
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
                    CreateRedisConnectionMultipleTimes(config, r.GetRequiredService<ILogger<Messaging>>()),
                    r.GetRequiredService<IServiceMessageDispatcher>(),
                    traceAction));
        }

        private static IServiceCollection AddRedisStorageInfrastructure(this IServiceCollection serviceCollection, RedisConfiguration config)
        {
            return serviceCollection.AddTransient<IStorageInfrastructure>(r =>
                new Storage(CreateRedisConnectionMultipleTimes(config, r.GetRequiredService<ILogger<Storage>>())));
        }
    }
}