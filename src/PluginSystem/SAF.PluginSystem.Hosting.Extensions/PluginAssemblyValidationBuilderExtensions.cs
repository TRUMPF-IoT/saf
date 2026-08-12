// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SAF.PluginSystem.Hosting.Extensions.Authenticode;

/// <summary>
/// Provides host-builder extensions for registering plugin assembly validators.
/// </summary>
public static class PluginAssemblyValidationBuilderExtensions
{
    /// <summary>
    /// Registers an additional plugin assembly validator in the execution chain.
    /// Validators are executed in registration order.
    /// </summary>
    /// <typeparam name="TValidator">The validator implementation type.</typeparam>
    /// <param name="hostBuilder">The plugin system host builder.</param>
    /// <returns>The same host builder instance.</returns>
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
    /// <param name="hostBuilder">The plugin system host builder.</param>
    /// <param name="configure">An optional callback for configuring the validator.</param>
    /// <returns>The same host builder instance.</returns>
    public static IPluginSystemHostBuilder AddStrongNamePluginAssemblyValidator(
        this IPluginSystemHostBuilder hostBuilder,
        Action<StrongNamePluginAssemblyValidatorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        hostBuilder.Services.AddOptions();
        var uniqueOptionsKey = $"{nameof(StrongNamePluginAssemblyValidatorOptions)}_{Guid.NewGuid():N}";
        hostBuilder.Services.Configure<StrongNamePluginAssemblyValidatorOptions>(
            uniqueOptionsKey,
            options => configure?.Invoke(options));
        hostBuilder.Services.AddSingleton<IPluginAssemblyValidator>(serviceProvider =>
            new StrongNamePluginAssemblyValidator(
                serviceProvider.GetRequiredService<IOptionsMonitor<StrongNamePluginAssemblyValidatorOptions>>(),
                uniqueOptionsKey));
        return hostBuilder;
    }

    /// <summary>
    /// Registers SAF's built-in digital-signature plugin assembly validator.
    /// </summary>
    /// <remarks>
    /// May be called repeatedly to define several trust domains; each call adds one validator with its own
    /// options, while the Authenticode services behind them are registered once and shared.
    /// </remarks>
    /// <param name="hostBuilder">The plugin system host builder.</param>
    /// <param name="configure">An optional callback for configuring the validator.</param>
    /// <returns>The same host builder instance.</returns>
    public static IPluginSystemHostBuilder AddDigitalSignaturePluginAssemblyValidator(
        this IPluginSystemHostBuilder hostBuilder,
        Action<DigitalSignaturePluginAssemblyValidatorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        hostBuilder.Services.AddOptions();
        var uniqueOptionsKey = $"{nameof(DigitalSignaturePluginAssemblyValidatorOptions)}_{Guid.NewGuid():N}";
        hostBuilder.Services.Configure<DigitalSignaturePluginAssemblyValidatorOptions>(
            uniqueOptionsKey,
            options => configure?.Invoke(options));
        hostBuilder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<DigitalSignaturePluginAssemblyValidatorOptions>,
                DigitalSignaturePluginAssemblyValidatorOptionsValidator>());
        // The Authenticode services carry no per-registration state, so a second trust domain must reuse
        // them instead of duplicating the whole object graph. Only the validator below is per-call, since
        // it is the one bound to this call's options.
        hostBuilder.Services.TryAddSingleton<IAuthenticodeCertificateTableParser, AuthenticodeCertificateTableParser>();
        hostBuilder.Services.TryAddSingleton<IAuthenticodeChainTrustVerifier>(_ =>
            OperatingSystem.IsWindows()
                ? new WindowsAuthenticodeTrustVerifier()
                : new CrossPlatformAuthenticodeTrustVerifier());
        hostBuilder.Services.TryAddSingleton<IAuthenticodePeHasher, AuthenticodePeHasher>();
        hostBuilder.Services.TryAddSingleton<IAuthenticodeSignatureReader, AuthenticodeSignatureReader>();
        hostBuilder.Services.AddSingleton<IPluginAssemblyValidator>(serviceProvider =>
            new DigitalSignaturePluginAssemblyValidator(
                serviceProvider.GetRequiredService<IOptionsMonitor<DigitalSignaturePluginAssemblyValidatorOptions>>(),
                uniqueOptionsKey,
                serviceProvider.GetRequiredService<IAuthenticodeSignatureReader>()));
        return hostBuilder;
    }
}