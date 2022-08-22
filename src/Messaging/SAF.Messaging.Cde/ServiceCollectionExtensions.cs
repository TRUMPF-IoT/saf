// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using nsCDEngine.Engines;
using nsCDEngine.Engines.ThingService;
using SAF.Common;
using SAF.Communication.Cde;
using SAF.Communication.PubSub.Cde;
using SAF.Communication.PubSub.Interfaces;

namespace SAF.Messaging.Cde
{
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

        public static IServiceCollection AddCdeMessagingInfrastructure(this IServiceCollection collection, Action<Message> traceAction = null)
            => collection.AddCdePubSubServices()
                .AddTransient<ICdeMessagingInfrastructure>(sp =>
                    new Messaging(sp.GetService<ILogger<Messaging>>(),
                        sp.GetRequiredService<IServiceMessageDispatcher>(),
                        sp.GetRequiredService<IPublisher>(),
                        sp.GetRequiredService<ISubscriber>(),
                        traceAction));

        public static IServiceCollection AddCdeStorageInfrastructure(this IServiceCollection collection)
            => collection.AddSingleton<IStorageInfrastructure, Storage>(sp =>
            {
                _ = sp.GetRequiredService<CdeApplication>();
                return new Storage(sp.GetService<ILogger<Storage>>());
            });

        public static IServiceCollection AddCdeInfrastructure(this IServiceCollection collection, Action<CdeConfiguration> configure, Action<Message> traceAction = null)
        {
            return collection.AddCde(configure)
                .AddCdeMessagingInfrastructure(traceAction)
                .AddCdeStorageInfrastructure()
                .AddSingleton<IMessagingInfrastructure>(sp => sp.GetRequiredService<ICdeMessagingInfrastructure>());
        }

        internal static IServiceCollection AddCdeMessagingInfrastructure(this IServiceCollection collection, MessagingConfiguration config)
        {
            collection.AddCdePubSubServices()
                .AddTransient(sp => new Func<MessagingConfiguration, ICdeMessagingInfrastructure>(cfg =>
                    new Messaging(sp.GetService<ILogger<Messaging>>(),
                        sp.GetRequiredService<IServiceMessageDispatcher>(),
                        sp.GetRequiredService<IPublisher>(),
                        sp.GetRequiredService<ISubscriber>(),
                        null,
                        new CdeMessagingConfiguration(cfg))))
                .AddTransient(sp => sp.GetRequiredService<Func<MessagingConfiguration, ICdeMessagingInfrastructure>>().Invoke(config));

            return collection;
        }

        private static IServiceCollection AddCdePubSubServices(this IServiceCollection collection)
        {
            collection.AddSingleton(sp =>
            {
                _ = sp.GetRequiredService<CdeApplication>();

                var engines = TheThingRegistry.GetBaseEngines(false);
                var engine = engines.FirstOrDefault(e => e.GetEngineName() == InfrastructureEngine);
                if (engine != null) return engine.GetBaseThing();

                if(!TheCDEngines.RegisterNewMiniRelay(InfrastructureEngine))
                    throw new InvalidOperationException("Failed to register CDE infrastructure engine");

                engine = TheThingRegistry.GetBaseEngine(InfrastructureEngine, false);
                return engine.GetBaseThing();
            });

            collection.AddSingleton(sp => new Publisher(Operator.GetLine(sp.GetRequiredService<TheThing>())).ConnectAsync().Result as Publisher)
                .AddSingleton(sp => sp.GetRequiredService<Publisher>() as IPublisher);
            collection.AddSingleton(sp => new Subscriber(Operator.GetLine(sp.GetRequiredService<TheThing>()), sp.GetRequiredService<Publisher>()))
                .AddSingleton(sp => sp.GetRequiredService<Subscriber>() as ISubscriber);

            return collection;
        }
    }
}