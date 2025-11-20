// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SAF.Hosting;
using SAF.Hosting.Diagnostics;
using SAF.Messaging.Redis;

Console.Title = "SAF Redis Test Host";

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddHost(context.Configuration.GetSection("ServiceHost").Bind)
            .AddHostDiagnostics();
        services.AddRedisInfrastructure(context.Configuration.GetSection("Redis").Bind);
    })
    .Build();

await host.RunAsync();