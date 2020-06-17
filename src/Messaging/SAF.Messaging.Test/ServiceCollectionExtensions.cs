// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAF.Common;

namespace SAF.Messaging.Test
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTestMessagingInfrastructure(this IServiceCollection serviceCollection, Action<Message> traceAction = null)
            => serviceCollection.AddSingleton<IMessagingInfrastructure>(r => 
                new TestMessaging(r.GetService<ILogger<TestMessaging>>(), r.GetService<IServiceMessageDispatcher>(), traceAction));
    }
}