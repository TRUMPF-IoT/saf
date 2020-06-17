// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAF.Messaging.Routing;
using System;
using System.IO;
using SAF.Hosting;
using SAF.Messaging.Cde;
using SAF.Messaging.Redis;

namespace TestRunnerMessageRouting
{
    class Program
    {
        static void Main(string[] args)
        {
            var environment = GetEnvironment();

            Console.Title = "SAF Message Routing Test Host";

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
            applicationServices.AddHost(config.GetSection("ServiceHost").Bind, mainLogger)
                .AddHostDiagnostics()
                .AddCde(config.GetSection("Cde").Bind)
                .AddRoutingMessagingInfrastructure(config.GetSection("MessageRouting").Bind)
                .AddRedisStorageInfrastructure(config.GetSection("Redis").Bind);

            using(var applicationServiceProvider = applicationServices.BuildServiceProvider())
            {
                applicationServiceProvider.UseCde()
                    .UseServiceHost()
                    .UseServiceHostDiagnostics();

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
