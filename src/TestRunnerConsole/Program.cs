// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SAF.Common;
using SAF.Hosting;
using SAF.Hosting.Diagnostics;
using SAF.Messaging.InProcess;

Console.Title = "SAF InProcess Test Host";

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddHost(context.Configuration.GetSection("ServiceHost").Bind)
            .AddHostDiagnostics();

        services.AddInProcessMessagingInfrastructure()
            .AddSingleton<IMessagingInfrastructure>(sp => sp.GetRequiredService<IInProcessMessagingInfrastructure>());
    })
    .Build();

await host.RunAsync();