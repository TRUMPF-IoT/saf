// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using Microsoft.Extensions.DependencyInjection;
using SAF.Hosting.Diagnostics;

namespace SAF.Hosting
{
    public static class ServiceProviderExtensions
    {
        public static IServiceProvider UseServiceHost(this IServiceProvider serviceProvider)
        {
            var serviceHost = serviceProvider.GetService<ServiceHost>();
            serviceHost.StartServices();

            return serviceProvider;
        }

        public static IServiceProvider UseServiceHostDiagnostics(this IServiceProvider serviceProvider)
        {
            var serviceHost = serviceProvider.GetRequiredService<ServiceHostDiagnostics>();
            serviceHost.StartDiagnostic();

            return serviceProvider;
        }
    }
}