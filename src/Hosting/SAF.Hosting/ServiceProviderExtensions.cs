// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAF.Common;
using SAF.Hosting.Diagnostics;

namespace SAF.Hosting
{
    public static class ServiceProviderExtensions
    {
        public static IServiceProvider UseServiceHost(this IServiceProvider serviceProvider)
        {
            ILogger logger = serviceProvider.GetService<ILogger<ServiceHost>>();
            int testCount = 10;
            while (!serviceProvider.IsConnected())
            {
                testCount--;
                logger.LogInformation($"Not yet connected, remaining tries: {testCount}");
                if (testCount == 0) throw new ApplicationException("Redis is not available");
                System.Threading.Thread.Sleep(500);
            }
            logger.LogInformation("connected to storage");

            var serviceHost = serviceProvider.GetService<ServiceHost>();
            serviceHost.StartServices();

            return serviceProvider;
        }

        private static bool IsConnected(this IServiceProvider sp)
        {
            try
            {
                const string storageKey = "saf/hostid";
                var storage = sp.GetService<IStorageInfrastructure>();
                storage?.GetString(storageKey);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static IServiceProvider UseServiceHostDiagnostics(this IServiceProvider serviceProvider)
        {
            var serviceHost = serviceProvider.GetRequiredService<ServiceHostDiagnostics>();
            serviceHost.StartDiagnostic();

            return serviceProvider;
        }
    }
}