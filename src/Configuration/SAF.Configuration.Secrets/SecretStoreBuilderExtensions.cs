// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets;

using Microsoft.Extensions.DependencyInjection;
using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// Fluent provider registration for the secret store. Each call appends a provider; the order of the
/// calls is the priority order used by <see cref="SecretStoreOptions.AutoProviderName"/> selection.
/// </summary>
public static class SecretStoreBuilderExtensions
{
    /// <summary>
    /// Adds the Windows Credential Manager provider (no-op on non-Windows platforms).
    /// </summary>
    public static ISecretStoreBuilder AddWindowsCredentialManager(this ISecretStoreBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddWindowsCredentialManagerSecretStore();
        return builder;
    }

    /// <summary>
    /// Adds a custom <see cref="ISecretStoreProvider"/> implementation, enabling additional backends
    /// (e.g. a remote key vault) without modifying the framework.
    /// </summary>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    public static ISecretStoreBuilder AddProvider<TProvider>(this ISecretStoreBuilder builder)
        where TProvider : class, ISecretStoreProvider
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<ISecretStoreProvider, TProvider>();
        return builder;
    }

    /// <summary>
    /// Adds all built-in providers for the current platform in the documented default priority:
    /// the OS-native store first, then the cross-platform file store. Each provider is a no-op when
    /// it is not applicable to the current platform, so the effective order per platform is
    /// native-store-then-file.
    /// </summary>
    public static ISecretStoreBuilder AddDefaults(this ISecretStoreBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddWindowsCredentialManager();
        // Future built-ins are appended here in priority order (e.g. systemd-creds, then the file store),
        // so an OS-native store is always preferred over the file store under auto-selection.
        return builder;
    }
}
