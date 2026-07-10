// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Extensions;

using Microsoft.Extensions.DependencyInjection;
using SAF.Messaging.Contracts;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMessageHandlerResolver(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IMessageHandlerResolver, MessageHandlerResolver>();

        return services;
    }
}
