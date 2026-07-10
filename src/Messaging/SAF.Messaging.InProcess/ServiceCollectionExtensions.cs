// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAF.Common;
using SAF.Messaging.Contracts;

[assembly: InternalsVisibleTo("SAF.Messaging.InProcess.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace SAF.Messaging.InProcess;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInProcessMessagingInfrastructure(this IServiceCollection serviceCollection, Action<Message>? traceAction = null)
        => serviceCollection.AddTransient<IInProcessMessagingInfrastructure>(r =>
            new InProcessMessaging(r.GetService<ILogger<InProcessMessaging>>(), ResolveMessageDispatcher(r), traceAction, r));

    internal static IServiceCollection AddInProcessMessagingInfrastructure(this IServiceCollection serviceCollection, MessagingConfiguration config)
    {
        serviceCollection.AddTransient(sp => new Func<MessagingConfiguration, IInProcessMessagingInfrastructure>(_ =>
            new InProcessMessaging(sp.GetService<ILogger<InProcessMessaging>>(), ResolveMessageDispatcher(sp), null, sp)));

        return serviceCollection.AddTransient(sp => sp.GetRequiredService<Func<MessagingConfiguration, IInProcessMessagingInfrastructure>>().Invoke(config));
    }

    private static IServiceMessageDispatcher ResolveMessageDispatcher(IServiceProvider serviceProvider)
        => serviceProvider.GetService<IServiceMessageDispatcher>() ??
           throw new InvalidOperationException("IServiceMessageDispatcher is not available. Ensure SAF.Messaging.Runtime is loaded as a plugin and SAF.Messaging.Contracts.dll is included in PluginContractsSearchPattern.");
}

