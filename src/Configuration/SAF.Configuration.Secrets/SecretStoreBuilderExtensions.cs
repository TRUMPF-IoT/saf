// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// Adds the cross-platform file-based provider. It requires an <see cref="ISecretProtector"/> to be
    /// registered separately (see <see cref="SecretStoreServiceCollectionExtensions.AddFileSecretStore"/>),
    /// which is why it is opt-in rather than part of <see cref="AddDefaults"/>.
    /// </summary>
    public static ISecretStoreBuilder AddFile(
        this ISecretStoreBuilder builder, Action<FileSecretStoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddFileSecretStore(configure);
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
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ISecretStoreProvider, TProvider>());
        return builder;
    }

    /// <summary>
    /// Adds the built-in default provider for the current platform: the Windows Credential Manager on
    /// Windows, and the cross-platform file store on other platforms, so auto-selection resolves a
    /// working default everywhere. On Windows the file store is not added by default (add it via
    /// <see cref="AddFile"/> if needed); on other platforms it is the default and therefore requires a
    /// consumer-registered <see cref="ISecretProtector"/>, as there is no OS-integrated at-rest
    /// encryption to fall back on.
    /// </summary>
    public static ISecretStoreBuilder AddDefaults(this ISecretStoreBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddWindowsCredentialManager();
        if (!OperatingSystem.IsWindows())
        {
            // No OS-native default is wired up off Windows yet (a read-only systemd-creds provider is
            // planned as a higher-priority native option), so the file store is the platform default there.
            builder.AddFile();
        }

        return builder;
    }
}
