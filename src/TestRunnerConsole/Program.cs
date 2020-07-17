// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAF.Common;
using SAF.Hosting;
using SAF.Messaging.InProcess;

namespace TestRunnerConsole
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var environment = GetEnvironment();

            Console.Title = "SAF Test Host" + (environment == "production" ? "" : $" ({environment})");

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
                .Build();

            var applicationServices = new ServiceCollection();
            applicationServices.AddLogging(l => l.AddConfiguration(config.GetSection("Logging")).AddConsole());

            var baseServiceProvider = applicationServices.BuildServiceProvider();
            var mainLogger = baseServiceProvider.GetService<ILogger<Program>>();
            mainLogger.LogInformation("Starting test runner console app...");

            applicationServices.AddConfiguration(config);
            applicationServices.AddHost(config.GetSection("ServiceHost").Bind, mainLogger);
            applicationServices.AddHostDiagnostics();
            applicationServices.AddInProcessMessagingInfrastructure()
                .AddSingleton<IMessagingInfrastructure>(sp => sp.GetRequiredService<IInProcessMessagingInfrastructure>());

            using(var applicationServiceProvider = applicationServices.BuildServiceProvider())
            {
                applicationServiceProvider.UseServiceHost();
                applicationServiceProvider.UseServiceHostDiagnostics();

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