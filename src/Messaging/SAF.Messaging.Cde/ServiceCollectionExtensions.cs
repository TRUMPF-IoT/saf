// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Cde;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using nsCDEngine.Engines;
using nsCDEngine.Engines.ThingService;
using SAF.Common;
using SAF.Messaging.Contracts;
using SAF.Communication.Cde;
using SAF.Communication.PubSub.Cde;
using Communication.PubSub.Interfaces;

public static class ServiceCollectionExtensions
{
    private const string InfrastructureEngine = "SAF.Messaging.Cde";

    public static IServiceCollection AddCde(this IServiceCollection collection, Action<CdeConfiguration> configure)
    {
        var config = new CdeConfiguration();
        configure?.Invoke(config);

        return collection.AddSingleton(sp => config)
            .AddSingleton(sp =>
            {
                var cdeApp = new CdeApplication(sp.GetService<ILogger<CdeApplication>>(), sp.GetRequiredService<CdeConfiguration>());
                cdeApp.Start();
                return cdeApp;
            });
    }

    public static IServiceCollection AddCdeMessagingInfrastructure(this IServiceCollection collection)
        => collection.AddCdePubSubServices()
            .AddKeyedSingleton<IMessagingInfrastructureFactory>(MessagingInfrastructureKeys.Cde,
                (sp, _) => new DelegatingMessagingInfrastructureFactory(
                    MessagingInfrastructureKeys.Cde,
                    cfg => CreateMessagingInfrastructure(sp, cfg)));

    public static IServiceCollection AddCdeStorageInfrastructure(this IServiceCollection collection)
        => collection.AddSingleton<IStorageInfrastructure, Storage>(sp =>
        {
            _ = sp.GetRequiredService<CdeApplication>();
            return new Storage(sp.GetService<ILogger<Storage>>());
        });

    public static IServiceCollection AddCdeInfrastructure(this IServiceCollection collection, Action<CdeConfiguration> configure)
    {
        return collection.AddCde(configure)
            .AddCdeMessagingInfrastructure()
            .AddCdeStorageInfrastructure();
    }

    private static Messaging CreateMessagingInfrastructure(IServiceProvider serviceProvider, MessagingConfiguration config)
        => new Messaging(serviceProvider.GetService<ILogger<Messaging>>(),
            ResolveMessageDispatcher(serviceProvider),
            serviceProvider.GetRequiredService<IPublisher>(),
            serviceProvider.GetRequiredService<ISubscriber>(),
            config.Config is null || config.Config.Count == 0 ? new CdeMessagingConfiguration() : new CdeMessagingConfiguration(config));

    private static IServiceMessageDispatcher ResolveMessageDispatcher(IServiceProvider serviceProvider)
        => serviceProvider.GetService<IServiceMessageDispatcher>() ??
           throw new InvalidOperationException("IServiceMessageDispatcher is not available. Ensure SAF.Messaging.Runtime is loaded as a plugin and SAF.Messaging.Contracts.dll is included in PluginContractsSearchPattern.");

    private static IServiceCollection AddCdePubSubServices(this IServiceCollection collection)
    {
        collection.AddSingleton(sp =>
        {
            _ = sp.GetRequiredService<CdeApplication>();

            var engines = TheThingRegistry.GetBaseEngines(false);
            var engine = engines.Find(e => e.GetEngineName() == InfrastructureEngine);
            if (engine != default(IBaseEngine)) return engine.GetBaseThing();

            if(!TheCDEngines.RegisterNewMiniRelay(InfrastructureEngine))
                throw new InvalidOperationException("Failed to register CDE infrastructure engine");

            engine = TheThingRegistry.GetBaseEngine(InfrastructureEngine, false);
            return engine.GetBaseThing();
        });

        collection.AddSingleton(sp =>
            {
                var publisher = new Publisher(Operator.GetLine(sp.GetRequiredService<TheThing>()));
                publisher.ConnectAsync().Wait();
                return publisher;
            })
            .AddSingleton<IPublisher>(sp => sp.GetRequiredService<Publisher>());
        collection.AddSingleton(sp => new Subscriber(Operator.GetLine(sp.GetRequiredService<TheThing>()), sp.GetRequiredService<IPublisher>()))
            .AddSingleton<ISubscriber>(sp => sp.GetRequiredService<Subscriber>());

        return collection;
    }
}


