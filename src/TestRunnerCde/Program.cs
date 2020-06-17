// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAF.Common;
using SAF.Hosting;
using SAF.Messaging.Cde;
using SAF.Messaging.Cde.Diagnostics;

namespace TestRunnerCde
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var environment = GetEnvironment();

            Console.Title = "SAF CDE Test Host";

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile($"appsettings_{environment}.json", optional: true, reloadOnChange: false)
                .Build();

            var applicationServices = new ServiceCollection();
            applicationServices.AddLogging(l => l.AddConfiguration(config.GetSection("Logging")).AddConsole());

            var baseServiceProvider = applicationServices.BuildServiceProvider();
            var mainLogger = baseServiceProvider.GetService<ILogger<Program>>();
            mainLogger.LogInformation("Starting test runner console app...");

            applicationServices.AddConfiguration(config);
            applicationServices.AddHost(config.GetSection("ServiceHost").Bind, hi =>
                {
                    hi.ServiceHostType = "SAF Test Host";
                    hi.FileSystemUserBasePath = Path.Combine(Directory.GetCurrentDirectory(), "saf");
                }, mainLogger);
            applicationServices.AddHostDiagnostics();
            applicationServices.AddCdeDiagnostics();
            applicationServices.AddCdeInfrastructure(config.GetSection("Cde").Bind);
            applicationServices.AddSingleton<IMessagingInfrastructure>(sp => sp.GetRequiredService<ICdeMessagingInfrastructure>());

            using(var applicationServiceProvider = applicationServices.BuildServiceProvider())
            {
                applicationServiceProvider.UseCde()
                    .UseServiceHost()
                    .UseServiceHostDiagnostics()
                    .UseCdeServiceHostDiagnostics();

                Console.ReadLine();
            }
        }

        private static string GetEnvironment()
        {
            var environment = "production";

            var envVarEnvironment = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT");

            if(!string.IsNullOrEmpty(envVarEnvironment))
                environment = envVarEnvironment;

            return environment;
        }
    }
}