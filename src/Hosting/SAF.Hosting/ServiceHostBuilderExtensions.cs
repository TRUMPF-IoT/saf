using Microsoft.Extensions.DependencyInjection;
using SAF.Common;
using SAF.Hosting.Abstractions;
using SAF.Hosting.Diagnostics;

namespace SAF.Hosting;

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

    public static IServiceHostBuilder AddServiceAssembly(this IServiceHostBuilder builder, IServiceAssemblyManifest serviceAssemblyManifest)
    {
        builder.Services.AddSingleton(serviceAssemblyManifest);
        return builder;
    }

    public static IServiceHostBuilder AddHostDiagnostics(this IServiceHostBuilder builder)
    {
        builder.Services.AddHostedService<ServiceHostDiagnostics>();
        return builder;
    }

    public static IServiceHostBuilder AddCommonSingletonService(this IServiceHostBuilder builder, Type serviceType, Type implementationType)
    {
        builder.Services.AddSingleton(serviceType, implementationType);
        builder.CommonServices.AddSingleton(serviceType, implementationType);
        return builder;
    }

    public static IServiceHostBuilder AddCommonSingletonService<TService, TImplementation>(this IServiceHostBuilder builder)
        where TService : class where TImplementation : class, TService
        => builder.AddCommonSingletonService(typeof(TService), typeof(TImplementation));

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