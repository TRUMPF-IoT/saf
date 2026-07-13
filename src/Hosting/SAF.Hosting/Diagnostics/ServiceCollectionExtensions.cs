// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Common.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.IO.Abstractions;
using Testably.Abstractions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds diagnostics collection at host startup.
    /// Registers <see cref="RealFileSystem"/> as <see cref="IFileSystem"/> unless one is already registered.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddHostDiagnostics(this IServiceCollection services)
    {
        services.TryAddSingleton<IFileSystem, RealFileSystem>();
        services.AddHostedService<ServiceHostDiagnostics>();
        return services;
    }
}
