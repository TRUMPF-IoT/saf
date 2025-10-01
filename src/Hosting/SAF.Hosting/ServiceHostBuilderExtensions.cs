// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;
using Contracts;
using Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.IO.Abstractions;

public static class ServiceHostBuilderExtensions
{
    public static IServiceHostBuilder AddServiceAssemblySearch(this IServiceHostBuilder builder)
        => builder.AddServiceAssemblySearch(_ => { });

    public static IServiceHostBuilder AddServiceAssemblySearch(this IServiceHostBuilder builder, Action<ServiceAssemblySearchOptions> setupAction)
    {
        builder.Services.Configure(setupAction);
        builder.Services.PostConfigure<ServiceAssemblySearchOptions>(ValidateServiceAssemblySearchOptions);

        builder.Services.AddSingleton<IServiceAssemblySearch, ServiceAssemblySearch>();

        return builder;
    }

    public static IServiceHostBuilder AddServiceAssembly<T>(this IServiceHostBuilder builder) where T : class, IServiceAssemblyManifest
    {
        builder.Services.AddSingleton<IServiceAssemblyManifest, T>();
        return builder;
    }

    public static IServiceHostBuilder AddHostDiagnostics(this IServiceHostBuilder builder)
    {
        builder.Services.TryAddTransient<IFileSystem, FileSystem>();
        builder.Services.AddHostedService<ServiceHostDiagnostics>();
        return builder;
    }

    private static void ValidateServiceAssemblySearchOptions(ServiceAssemblySearchOptions options)
    {
        const string errorLogFormat = "Configuration setting \"{0}\" not set!";

        string? missingConfig = null;
        if (string.IsNullOrWhiteSpace(options.BasePath))
            missingConfig = nameof(options.BasePath);
        else if (string.IsNullOrWhiteSpace(options.SearchFilenamePattern))
            missingConfig = nameof(options.SearchFilenamePattern);
        else if (string.IsNullOrWhiteSpace(options.SearchPath))
            missingConfig = nameof(options.SearchPath);

        if (!string.IsNullOrWhiteSpace(missingConfig))
        {
            var error = string.Format(errorLogFormat, missingConfig);
            throw new InvalidOperationException(error);
        }
    }
}