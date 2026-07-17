// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
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

        var options = new StrongNamePluginAssemblyValidatorOptions();
        configure?.Invoke(options);

        hostBuilder.Services.AddSingleton<IPluginAssemblyValidator>(_ => new StrongNamePluginAssemblyValidator(options));
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

        var options = new DigitalSignaturePluginAssemblyValidatorOptions();
        configure?.Invoke(options);

        hostBuilder.Services.AddSingleton<IAuthenticodeSignatureReader, AuthenticodeSignatureReader>();
        hostBuilder.Services.AddSingleton<IPluginAssemblyValidator>(sp =>
            new DigitalSignaturePluginAssemblyValidator(options, sp.GetRequiredService<IAuthenticodeSignatureReader>()));
        return hostBuilder;
    }
}