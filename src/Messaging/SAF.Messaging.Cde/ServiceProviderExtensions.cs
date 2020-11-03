// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using Microsoft.Extensions.DependencyInjection;

namespace SAF.Messaging.Cde
{
    public static class ServiceProviderExtensions
    {
        public static IServiceProvider UseCde(this IServiceProvider serviceProvider)
        {
            _ = serviceProvider.GetService<CdeApplication>();
            return serviceProvider;
        }
    }
}