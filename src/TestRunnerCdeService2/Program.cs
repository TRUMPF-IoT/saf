// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SAF.Common;
using SAF.Hosting;
using SAF.Hosting.Diagnostics;
using SAF.Messaging.Cde;
using SAF.Messaging.Cde.Diagnostics;

Console.Title = "SAF CDE Test Service2";

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddCdeDiagnostics();
        services.AddCdeInfrastructure(context.Configuration.GetSection("Cde").Bind);
        services.AddSingleton<IMessagingInfrastructure>(sp => sp.GetRequiredService<ICdeMessagingInfrastructure>());

        services.AddHost(context.Configuration.GetSection("ServiceHost").Bind)
            .AddHostDiagnostics();
    })
    .Build();

await host.RunAsync();