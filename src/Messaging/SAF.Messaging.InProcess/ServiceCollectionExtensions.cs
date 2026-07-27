// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAF.Messaging.Contracts;

[assembly: InternalsVisibleTo("SAF.Messaging.InProcess.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace SAF.Messaging.InProcess;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInProcessMessagingInfrastructure(this IServiceCollection serviceCollection)
        => serviceCollection
            .AddKeyedSingleton<IMessagingInfrastructureFactory>(MessagingInfrastructureKeys.InProcess,
                (sp, _) => new DelegatingMessagingInfrastructureFactory(
                    MessagingInfrastructureKeys.InProcess,
                    cfg => CreateMessagingInfrastructure(sp)));

    private static InProcessMessaging CreateMessagingInfrastructure(IServiceProvider serviceProvider)
        => new InProcessMessaging(serviceProvider.GetService<ILogger<InProcessMessaging>>(), ResolveMessageDispatcher(serviceProvider));

    private static IServiceMessageDispatcher ResolveMessageDispatcher(IServiceProvider serviceProvider)
        => serviceProvider.GetService<IServiceMessageDispatcher>() ??
           throw new InvalidOperationException("IServiceMessageDispatcher is not available. Ensure SAF.Messaging.Runtime is loaded as a plugin and SAF.Messaging.Contracts.dll is included in PluginContractsSearchPattern.");
}

