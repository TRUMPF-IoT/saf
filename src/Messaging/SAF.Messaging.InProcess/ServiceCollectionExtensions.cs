// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAF.Common;

[assembly: InternalsVisibleTo("SAF.Messaging.InProcess.Tests")]

namespace SAF.Messaging.InProcess
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInProcessMessagingInfrastructure(this IServiceCollection serviceCollection, Action<Message> traceAction = null)
            => serviceCollection.AddTransient<IInProcessMessagingInfrastructure>(r =>
                new InProcessMessaging(r.GetService<ILogger<InProcessMessaging>>(), r.GetRequiredService<IServiceMessageDispatcher>(), traceAction));

        internal static IServiceCollection AddInProcessMessagingInfrastructure(this IServiceCollection serviceCollection, MessagingConfiguration config)
        {
            serviceCollection.AddTransient(sp => new Func<MessagingConfiguration, IInProcessMessagingInfrastructure>(cfg =>
                new InProcessMessaging(sp.GetService<ILogger<InProcessMessaging>>(), sp.GetRequiredService<IServiceMessageDispatcher>())));

            return serviceCollection.AddTransient(sp => sp.GetRequiredService<Func<MessagingConfiguration, IInProcessMessagingInfrastructure>>().Invoke(config));
        }
    }
}