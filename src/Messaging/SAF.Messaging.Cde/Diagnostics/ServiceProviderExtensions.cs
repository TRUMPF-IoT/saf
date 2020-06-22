// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using Microsoft.Extensions.DependencyInjection;

namespace SAF.Messaging.Cde.Diagnostics
{
    public static class ServiceProviderExtensions
    {
        public static IServiceProvider UseCdeServiceHostDiagnostics(this IServiceProvider serviceProvider)
        {
            var diag = serviceProvider.GetService<ServiceHostDiagnostics>();
            diag.CollectInformation();

            return serviceProvider;
        }
    }
}
