// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.DependencyInjection;

namespace SAF.Messaging.Cde.Diagnostics;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCdeDiagnostics(this IServiceCollection collection)
        => collection.AddHostedService<ServiceHostDiagnostics>();
}