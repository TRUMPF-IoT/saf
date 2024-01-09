// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Cde.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCdeDiagnostics(this IServiceCollection collection)
        => collection.AddHostedService<ServiceHostDiagnostics>();
}