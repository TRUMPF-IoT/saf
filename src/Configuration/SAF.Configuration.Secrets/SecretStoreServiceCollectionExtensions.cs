// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SAF.Configuration.Secrets.Contracts;
using SAF.Configuration.Secrets.WindowsCredentialManager;

/// <summary>
/// Registers the secret store and its providers in a plain <see cref="IServiceCollection"/>, so the
/// feature can be used with or without the SAF plugin system.
/// </summary>
public static class SecretStoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core secret store: binds <see cref="SecretStoreOptions"/> and registers the
    /// selecting <see cref="ISecretStore"/>. Use the returned <see cref="ISecretStoreBuilder"/> to add
    /// one or more providers (e.g. <c>.AddWindowsCredentialManager()</c> or <c>.AddDefaults()</c>).
    /// </summary>
    public static ISecretStoreBuilder AddSecretStore(this IServiceCollection services, Action<SecretStoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<ISecretStore, CompositeSecretStore>();
        return new SecretStoreBuilder(services);
    }

    /// <summary>
    /// Registers the Windows Credential Manager provider. A no-op on non-Windows platforms, so it is
    /// safe to call unconditionally; the selector then falls back to another available provider.
    /// </summary>
    public static IServiceCollection AddWindowsCredentialManagerSecretStore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (OperatingSystem.IsWindows())
        {
            // Instantiate inside the platform guard so the analyzer can prove the Windows-only type is
            // only ever created on Windows (a factory lambda would move the call outside the guard).
            services.TryAddSingleton<INativeCredentialApi>(new WindowsCredentialManagerNativeApi());
            services.AddSingleton<ISecretStoreProvider, WindowsCredentialManagerSecretStore>();
        }

        return services;
    }
}
