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
using SAF.PluginSystem.Hosting.Contracts;
using SAF.Communication.Cde;
using SAF.Communication.PubSub.Cde;
using Communication.PubSub.Interfaces;

public static class ServiceCollectionExtensions
{
    private const string InfrastructureEngine = "SAF.Messaging.Cde";

    /// <summary>
    /// Registers the <see cref="CdeConfiguration"/> and the <see cref="CdeApplication"/> built from it.
    /// </summary>
    /// <param name="collection">The container the C-DEngine services are registered in.</param>
    /// <param name="configure">Fills the configuration, typically by binding the <c>Cde</c> section.</param>
    /// <remarks>
    /// The configuration is built when it is first resolved, not when it is registered, and every
    /// <see cref="IPluginOptionsCustomizer{TOptions}"/> the host forwarded runs over it afterwards. That is
    /// the seam for values a host cannot put into configuration - an application id compiled into the host,
    /// a scope id or proxy password it decrypts itself - which in 10.x it passed to this method directly.
    /// </remarks>
    public static IServiceCollection AddCde(this IServiceCollection collection, Action<CdeConfiguration> configure)
    {
        return collection.AddSingleton(sp =>
            {
                var config = new CdeConfiguration();
                configure?.Invoke(config);

                return sp.ApplyPluginOptionsCustomizers(config);
            })
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


