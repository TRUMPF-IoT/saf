// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace TestRunnerNats;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SAF.Hosting;
using SAF.Messaging.Nats;
using SAF.Hosting.Diagnostics;

public static class Program
{
    static async Task Main(string[] args)
    {
        Console.Title = "SAF Nats Test Host";

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddHost(context.Configuration.GetSection("ServiceHost").Bind)
                    .AddHostDiagnostics();
                services.AddNatsInfrastructure(context.Configuration.GetSection("Nats").Bind);
            })
            .Build();

        await host.RunAsync();
    }
}
