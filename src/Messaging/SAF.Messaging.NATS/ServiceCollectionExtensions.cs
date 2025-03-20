using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Net;
using SAF.Common;

[assembly: InternalsVisibleTo("SAF.Messaging.Nats.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]


namespace SAF.Messaging.Nats;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNatsMessagingInfrastructure(this IServiceCollection serviceCollection, Action<NatsConfiguration> configure, Action<Message>? traceAction = null)
    {
        var config = new NatsConfiguration();
        configure(config);

        return serviceCollection.AddNatsMessagingInfrastructure(config, traceAction);
    }

    public static IServiceCollection AddNatsStorageInfrastructure(this IServiceCollection serviceCollection, Action<NatsConfiguration> configure)
    {
        var config = new NatsConfiguration();
        configure(config);

        return serviceCollection.AddNatsStorageInfrastructure(config);
    }


    public static IServiceCollection AddNatsInfrastructure(this IServiceCollection serviceCollection, Action<NatsConfiguration> configure, Action<Message>? traceAction = null)
    {
        var config = new NatsConfiguration();
        configure.Invoke(config);

        return serviceCollection.AddNatsMessagingInfrastructure(config, traceAction)
            .AddNatsStorageInfrastructure(config)
            .AddSingleton<IMessagingInfrastructure>(sp => sp.GetRequiredService<INatsMessagingInfrastructure>());
    }


    internal static IServiceCollection AddNatsMessagingInfrastructure(this IServiceCollection serviceCollection, MessagingConfiguration config)
    {
        serviceCollection
            .AddTransient(sp => new Func<MessagingConfiguration, INatsMessagingInfrastructure>(cfg =>
            {
                var natsCfg = CreateNatsConfiguration(cfg);
                return new Messaging(sp.GetRequiredService<ILogger<Messaging>>(),
                    CreateNatsClient(natsCfg, sp.GetRequiredService<ILogger<Messaging>>()),
                    new NatsSubscriptionManager(),
                    sp.GetRequiredService<IServiceMessageDispatcher>(),
                    null);
            }))
            .AddTransient(sp => sp.GetRequiredService<Func<MessagingConfiguration, INatsMessagingInfrastructure>>().Invoke(config));

        return serviceCollection;
    }

    private static NatsConfiguration CreateNatsConfiguration(MessagingConfiguration config)
    {
        var msgCfg = new NatsMessagingConfiguration(config);
        var natsCfg = new NatsConfiguration()
        {
            Url = msgCfg.Url ?? "",
            Verbose = msgCfg.Verbose,
            ConnectionTimeout = TimeSpan.FromSeconds(msgCfg.ConnectionTimeout),
            RequestTimeout = TimeSpan.FromSeconds(msgCfg.RequestTimeout),
            CommandTimeout = TimeSpan.FromSeconds(msgCfg.CommandTimeout),
            MaxReconnectRetry = msgCfg.MaxReconnectRetry,
            AuthOpts = new NatsConfigurationAuthOpts()
            {
                Username = msgCfg.Username,
                Password = msgCfg.Password,
                Token = msgCfg.Token,
                Jwt = msgCfg.Jwt,
                NKey = msgCfg.NKey,
                Seed = msgCfg.Seed,
                CredsFile = msgCfg.CredsFile,
                NKeyFile = msgCfg.NKeyFile
            }
        };

        return natsCfg;
    }

    private static INatsClient CreateNatsClient(NatsConfiguration config, ILogger logger)
    {
        var natsConfiguration = new NatsOpts()
        {
            Url = config.Url,
            Verbose = config.Verbose,
            CommandTimeout = config.CommandTimeout,
            RequestTimeout = config.RequestTimeout,
            MaxReconnectRetry = config.MaxReconnectRetry,
            AuthOpts = new NatsAuthOpts
            {
                Username = config.AuthOpts.Username,
                Password = config.AuthOpts.Password,
                Token = config.AuthOpts.Token,
                Jwt = config.AuthOpts.Jwt,
                NKey = config.AuthOpts.NKey,
                Seed = config.AuthOpts.Seed,
                CredsFile = config.AuthOpts.CredsFile,
                NKeyFile = config.AuthOpts.NKeyFile
            }
        };

        var natsClient = new NatsClient(natsConfiguration);
        try
        {
            natsClient.ConnectAsync().GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
        }

        return natsClient;
    }

    private static IServiceCollection AddNatsMessagingInfrastructure(this IServiceCollection serviceCollection, NatsConfiguration config, Action<Message>? traceAction)
    {
        return serviceCollection.AddTransient<INatsMessagingInfrastructure>(r =>
            new Messaging(r.GetRequiredService<ILogger<Messaging>>(),
                CreateNatsClient(config, r.GetRequiredService<ILogger<Messaging>>()),
                new NatsSubscriptionManager(),
                r.GetRequiredService<IServiceMessageDispatcher>(),
                traceAction));
    }

    private static IServiceCollection AddNatsStorageInfrastructure(this IServiceCollection serviceCollection, NatsConfiguration config)
    {
        return serviceCollection.AddTransient<IStorageInfrastructure>(r =>
        {
            var natsClient = CreateNatsClient(config, r.GetRequiredService<ILogger<Storage>>());
            return new Storage(natsClient.CreateObjectStoreContext());
        });
    }
}
