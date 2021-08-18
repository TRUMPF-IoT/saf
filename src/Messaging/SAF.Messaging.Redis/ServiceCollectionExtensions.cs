// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using SAF.Common;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SAF.Messaging.Redis.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

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
                        CreateRedisConnection(redisCfg, sp.GetRequiredService<ILogger<Messaging>>()).multiplexer,
                        sp.GetRequiredService<IServiceMessageDispatcher>(),
                        null);
                }))
                .AddTransient(sp => sp.GetRequiredService<Func<MessagingConfiguration, IRedisMessagingInfrastructure>>().Invoke(config));

            return serviceCollection;
        }

        private static (IConnectionMultiplexer multiplexer, ConfigurationOptions options) CreateRedisConnection(RedisConfiguration config, ILogger logger)
        {
            var timeoutInMs = config.Timeout > 0 ? config.Timeout : 40000;

            var options = ConfigurationOptions.Parse(config.ConnectionString);
            options.SetDefaultPorts();

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

            TaskCompletionSource<ConnectionMultiplexer> tcs = new(TaskCreationOptions.AttachedToParent);

            void ConnectionRestoredAction(object sender, ConnectionFailedEventArgs args)
            {
                if (((ConnectionMultiplexer) sender).IsConnected)
                {
                    tcs.TrySetResult((ConnectionMultiplexer) sender);
                }
            }

            using var ct = new CancellationTokenSource(timeoutInMs);
            using (ct.Token.Register(() =>
            {
                tcs.TrySetException(new TimeoutException($"Redis: Configured connect timeout of {timeoutInMs} ms exceeded"));
            }))
            {
                var conn = ConnectionMultiplexer.Connect(options);
                if (!conn.IsConnected)
                {
                    logger.LogInformation("Not yet connected to redis, wait");
                    conn.ConnectionRestored += ConnectionRestoredAction;
                }
                else
                {
                    tcs.TrySetResult(conn);
                    logger.LogInformation("Successfully connected to redis");
                }
                var connResult = tcs.Task.Result;
                connResult.ConnectionRestored -= ConnectionRestoredAction;
                connResult.ConnectionFailed += (_, _) =>
                {
                    logger.LogCritical("Connection to redis lost");
                };
                connResult.ConnectionRestored += (_, _) =>
                {
                    logger.LogCritical("Connection to redis established");
                };

                return (connResult, options);
            }
        }

        private static IServiceCollection AddRedisMessagingInfrastructure(this IServiceCollection serviceCollection, RedisConfiguration config, Action<Message> traceAction)
        {
            return serviceCollection.AddTransient<IRedisMessagingInfrastructure>(r =>
                new Messaging(r.GetRequiredService<ILogger<Messaging>>(),
                    CreateRedisConnection(config, r.GetRequiredService<ILogger<Messaging>>()).multiplexer,
                    r.GetRequiredService<IServiceMessageDispatcher>(),
                    traceAction));
        }

        private static IServiceCollection AddRedisStorageInfrastructure(this IServiceCollection serviceCollection, RedisConfiguration config)
        {
            return serviceCollection.AddTransient<IStorageInfrastructure>(r =>
            {
                var (muxer, options) = CreateRedisConnection(config, r.GetRequiredService<ILogger<Storage>>());
                return new Storage(muxer, options);
            });
        }
    }
}