// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SAF.Hosting;
using SAF.Messaging.Redis;

Console.Title = "SAF Redis Test Host";

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var loggingServices = new ServiceCollection();
        loggingServices.AddLogging(l => l.AddConfiguration(context.Configuration.GetSection("Logging")).AddConsole());
        using var loggingServiceProvider = loggingServices.BuildServiceProvider();
        var mainLogger = loggingServiceProvider.GetService<ILogger<Program>>();

        services.AddHost(context.Configuration.GetSection("ServiceHost").Bind, mainLogger);
        services.AddHostDiagnostics();
        services.AddRedisInfrastructure(context.Configuration.GetSection("Redis").Bind);
    })
    .Build();

await host.RunAsync();