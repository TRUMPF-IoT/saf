// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using SAF.Hosting.Diagnostics;

namespace SAF.Hosting;

public static class ServiceProviderExtensions
{
    [Obsolete("UseServiceHost is deprecated and will be removed in a future release. Use Microsoft.Extensions.Hosting.IHostBuilder and Microsoft.Extensions.Hosting.IHost to configure and run your SAF ServiceHost.")]
    public static IServiceProvider UseServiceHost(this IServiceProvider serviceProvider)
    {
        var serviceHost = serviceProvider.GetRequiredService<ServiceHost>();
        serviceHost.StartAsync(CancellationToken.None).Wait();

        return serviceProvider;
    }

    public static IServiceProvider UseServiceHostDiagnostics(this IServiceProvider serviceProvider)
    {
        var serviceHost = serviceProvider.GetRequiredService<ServiceHostDiagnostics>();
        serviceHost.StartDiagnostic();

        return serviceProvider;
    }
}