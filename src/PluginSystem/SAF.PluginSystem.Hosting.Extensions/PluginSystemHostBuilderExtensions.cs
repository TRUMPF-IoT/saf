// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SAF.PluginSystem.Hosting.Extensions.Authenticode;

public static class PluginSystemHostBuilderExtensions
{
    /// <summary>
    /// Registers an additional plugin assembly validator in the execution chain.
    /// Validators are executed in registration order.
    /// </summary>
    public static IPluginSystemHostBuilder AddPluginAssemblyValidator<TValidator>(this IPluginSystemHostBuilder hostBuilder)
        where TValidator : class, IPluginAssemblyValidator
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        hostBuilder.Services.AddSingleton<IPluginAssemblyValidator, TValidator>();
        return hostBuilder;
    }

    /// <summary>
    /// Registers SAF's built-in strong-name plugin assembly validator.
    /// </summary>
    public static IPluginSystemHostBuilder AddStrongNamePluginAssemblyValidator(
        this IPluginSystemHostBuilder hostBuilder,
        Action<StrongNamePluginAssemblyValidatorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        hostBuilder.Services.AddOptions();
        hostBuilder.Services.Configure<StrongNamePluginAssemblyValidatorOptions>(opts => configure?.Invoke(opts));
        hostBuilder.Services.AddSingleton<IPluginAssemblyValidator, StrongNamePluginAssemblyValidator>();
        return hostBuilder;
    }

    /// <summary>
    /// Registers SAF's built-in digital-signature plugin assembly validator.
    /// </summary>
    public static IPluginSystemHostBuilder AddDigitalSignaturePluginAssemblyValidator(
        this IPluginSystemHostBuilder hostBuilder,
        Action<DigitalSignaturePluginAssemblyValidatorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        hostBuilder.Services.AddOptions();
        hostBuilder.Services.Configure<DigitalSignaturePluginAssemblyValidatorOptions>(opts => configure?.Invoke(opts));
        hostBuilder.Services.AddSingleton<IAuthenticodeCertificateTableParser, AuthenticodeCertificateTableParser>();
        hostBuilder.Services.AddSingleton<IAuthenticodeChainTrustVerifier>(_ =>
            OperatingSystem.IsWindows()
                ? new WindowsAuthenticodeTrustVerifier()
                : new CrossPlatformAuthenticodeTrustVerifier());
        hostBuilder.Services.AddSingleton<IAuthenticodePeHasher>(serviceProvider =>
            new AuthenticodePeHasher(
                serviceProvider.GetRequiredService<IAuthenticodeCertificateTableParser>()));
        hostBuilder.Services.AddSingleton<IAuthenticodeSignatureReader>(serviceProvider =>
            new AuthenticodeSignatureReader(
                serviceProvider.GetRequiredService<IAuthenticodeChainTrustVerifier>(),
                serviceProvider.GetRequiredService<IAuthenticodePeHasher>(),
                serviceProvider.GetRequiredService<IAuthenticodeCertificateTableParser>()));
        hostBuilder.Services.AddSingleton<IPluginAssemblyValidator>(serviceProvider =>
            new DigitalSignaturePluginAssemblyValidator(
                serviceProvider.GetRequiredService<IOptions<DigitalSignaturePluginAssemblyValidatorOptions>>(),
                serviceProvider.GetRequiredService<IAuthenticodeSignatureReader>()));
        return hostBuilder;
    }
}