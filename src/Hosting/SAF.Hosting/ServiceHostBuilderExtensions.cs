// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;

using Contracts;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceHostBuilderExtensions
{
    /// <summary>
    /// Enables automatic discovery of service assemblies using default search settings.
    /// </summary>
    /// <remarks>This method configures the builder to search for service assemblies using default options. To
    /// customize the search behavior, use the overload that accepts a configuration delegate.</remarks>
    /// <param name="builder">The service host builder to configure with service assembly search capabilities. Cannot be null.</param>
    /// <returns>The same instance of <see cref="IServiceHostBuilder"/> to allow for method chaining.</returns>
    public static IServiceHostBuilder AddServiceAssemblySearch(this IServiceHostBuilder builder)
        => builder.AddServiceAssemblySearch(_ => { });

    /// <summary>
    /// Adds and configures service assembly search functionality to the service host builder.
    /// </summary>
    /// <remarks>This method registers the <see cref="IServiceAssemblySearch"/> service and allows
    /// customization of its options. Call this method during service host configuration to enable automatic discovery
    /// of service assemblies.</remarks>
    /// <param name="builder">The service host builder to which the service assembly search functionality will be added.</param>
    /// <param name="setupAction">An action to configure the options for service assembly search. This delegate is used to set up custom search
    /// behavior.</param>
    /// <returns>The same instance of <see cref="IServiceHostBuilder"/> to allow for method chaining.</returns>
    public static IServiceHostBuilder AddServiceAssemblySearch(this IServiceHostBuilder builder, Action<ServiceAssemblySearchOptions> setupAction)
    {
        builder.Services.Configure(setupAction);
        builder.Services.PostConfigure<ServiceAssemblySearchOptions>(ValidateServiceAssemblySearchOptions);

        builder.Services.AddSingleton<IServiceAssemblySearch, ServiceAssemblySearch>();

        return builder;
    }

    /// <summary>
    /// Registers a service assembly manifest of the specified type with the service host builder.
    /// </summary>
    /// <remarks>This method adds the specified service assembly manifest as a singleton service. Use this
    /// method to register custom service assemblies for discovery and configuration during host startup.</remarks>
    /// <typeparam name="T">The type of the service assembly manifest to register. Must implement the IServiceAssemblyManifest interface.</typeparam>
    /// <param name="builder">The service host builder to which the service assembly manifest will be added.</param>
    /// <returns>The same IServiceHostBuilder instance so that additional configuration calls can be chained.</returns>
    public static IServiceHostBuilder AddServiceAssembly<T>(this IServiceHostBuilder builder) where T : class, IServiceAssemblyManifest
    {
        builder.Services.AddSingleton<IServiceAssemblyManifest, T>();
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