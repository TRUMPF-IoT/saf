// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Diagnostics;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.IO.Abstractions;

public static class ServiceHostBuilderExtensions
{
    /// <summary>
    /// Adds diagnostic services to the service host builder, enabling host-level diagnostics and monitoring.
    /// </summary>
    /// <remarks>This method registers services required for host diagnostics, including file system access
    /// and a hosted diagnostics service. Call this method during service host configuration to enable diagnostics
    /// features.</remarks>
    /// <param name="builder">The service host builder to which diagnostic services will be added. Cannot be null.</param>
    /// <returns>The same instance of <see cref="IServiceHostBuilder"/> to allow for method chaining.</returns>
    public static IServiceHostBuilder AddHostDiagnostics(this IServiceHostBuilder builder)
    {
        builder.Services.TryAddTransient<IFileSystem, FileSystem>();
        builder.Services.AddHostedService<ServiceHostDiagnostics>();
        return builder;
    }
}