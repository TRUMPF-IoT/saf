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

    public static IServiceCollection AddMessageHandler<TMessageHandler>(this IServiceCollection services, ServiceLifetime lifetime)
        where TMessageHandler : class, IMessageHandler
    {
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton<TMessageHandler>();
                services.AddSingleton<IMessageHandler>(sp => sp.GetRequiredService<TMessageHandler>());
                break;
            case ServiceLifetime.Scoped:
                services.AddScoped<TMessageHandler>();
                services.AddScoped<IMessageHandler>(sp => sp.GetRequiredService<TMessageHandler>());
                break;
            case ServiceLifetime.Transient:
                services.AddTransient<TMessageHandler>();
                services.AddTransient<IMessageHandler>(sp => sp.GetRequiredService<TMessageHandler>());
                break;
        }

        return services;
    }

    public static IServiceCollection AddSingletonMessageHandler<TMessageHandler>(this IServiceCollection services)
        where TMessageHandler : class, IMessageHandler
        => services.AddMessageHandler<TMessageHandler>(ServiceLifetime.Singleton);

    public static IServiceCollection AddTransientMessageHandler<TMessageHandler>(this IServiceCollection services)
        where TMessageHandler : class, IMessageHandler
        => services.AddMessageHandler<TMessageHandler>(ServiceLifetime.Transient);
}
