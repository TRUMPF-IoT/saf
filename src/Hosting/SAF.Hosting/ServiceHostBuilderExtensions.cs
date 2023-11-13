using Microsoft.Extensions.DependencyInjection;

namespace SAF.Hosting;

public static class ServiceHostBuilderExtensions
{
    public static IServiceHostBuilder AddServiceAssemblySearch(this IServiceHostBuilder builder, Action<ServiceAssemblySearchOptions> setupAction)
    {
        builder.Services.Configure(setupAction);
        builder.Services.PostConfigure<ServiceAssemblySearchOptions>(ValidateAssemblySearchOptions);

        builder.Services.AddSingleton<IServiceAssemblySearch, ServiceAssemblySearch>();

        return builder;
    }

    private static void ValidateAssemblySearchOptions(ServiceAssemblySearchOptions options)
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