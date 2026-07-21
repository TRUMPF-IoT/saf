// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets;

using System.IO.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SAF.Configuration.Secrets.Contracts;
using SAF.Configuration.Secrets.FileStore;
using SAF.Configuration.Secrets.WindowsCredentialManager;
using Testably.Abstractions;

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
            // TryAddEnumerable keeps the registration idempotent, so calling this alongside
            // AddSecretConfigurationResolution does not register the provider twice.
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ISecretStoreProvider, WindowsCredentialManagerSecretStore>());
        }

        return services;
    }

    /// <summary>
    /// Registers the cross-platform file-based provider, which encrypts each value at rest through an
    /// <see cref="ISecretProtector"/>. That protector (and its key/certificate material) is intentionally
    /// not registered here: register one explicitly, e.g.
    /// <c>services.AddSingleton&lt;ISecretProtector&gt;(_ =&gt; new PkcsSecretProtector(certificate))</c>.
    /// A default <see cref="IFileSystem"/> is registered only if none exists yet.
    /// </summary>
    public static IServiceCollection AddFileSecretStore(
        this IServiceCollection services, Action<FileSecretStoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<IFileSystem, RealFileSystem>();
        // TryAddEnumerable keeps the registration idempotent, so combining this with other
        // registrations does not add the provider twice.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISecretStoreProvider, FileSecretStore>());
        return services;
    }
}
