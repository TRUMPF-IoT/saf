// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using SAF.Hosting;
using SAF.Messaging.Cde;
using SAF.Messaging.Redis;
using SAF.Messaging.Routing;
using SAF.Hosting.Diagnostics;

Console.Title = "SAF Message Routing Test Host";

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddCde(context.Configuration.GetSection("Cde").Bind)
            .AddRoutingMessagingInfrastructure(context.Configuration.GetSection("MessageRouting").Bind)
            .AddRedisStorageInfrastructure(context.Configuration.GetSection("Redis").Bind);

        services.AddHost(context.Configuration.GetSection("ServiceHost").Bind)
            .AddHostDiagnostics();
    })
    .Build();

await host.RunAsync();