// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SAF.Common;
using SAF.Hosting;
using SAF.Messaging.Cde;
using SAF.Messaging.Cde.Diagnostics;

Console.Title = "SAF CDE Test Host";

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var loggingServices = new ServiceCollection();
        loggingServices.AddLogging(l => l.AddConfiguration(context.Configuration.GetSection("Logging")).AddConsole());
        using var loggingServiceProvider = loggingServices.BuildServiceProvider();
        var mainLogger = loggingServiceProvider.GetService<ILogger<Program>>();

        services.AddCdeDiagnostics();
        services.AddCdeInfrastructure(context.Configuration.GetSection("Cde").Bind);
        services.AddSingleton<IMessagingInfrastructure>(sp => sp.GetRequiredService<ICdeMessagingInfrastructure>());

        services.AddHost(context.Configuration.GetSection("ServiceHost").Bind, mainLogger);
        services.AddHostDiagnostics();
    })
    .Build();

host.Services
    .UseServiceHostDiagnostics()
    .UseCdeServiceHostDiagnostics();

await host.RunAsync();