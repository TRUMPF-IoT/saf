// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Contracts;
/// <summary>
/// A pluggable secret store backend (e.g. Windows Credential Manager, an encrypted file, or
/// systemd credentials). Multiple providers can be registered; the active one is chosen by name
/// or by platform availability. New backends are added by implementing this interface, without
/// modifying the selector (open/closed principle).
/// </summary>
public interface ISecretStoreProvider : ISecretStore
{
    /// <summary>
    /// The stable, case-insensitive identifier of this provider (e.g. <c>"windows-credential-manager"</c>,
    /// <c>"file"</c>, <c>"systemd-creds"</c>). Used to select a provider explicitly via
    /// <see cref="SecretStoreOptions.ProviderName"/>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether this provider can be used in the current process on the current platform/environment.
    /// The selector skips unavailable providers when resolving <see cref="SecretStoreOptions.ProviderName"/>
    /// set to <c>"auto"</c>.
    /// </summary>
    bool IsAvailable { get; }
}
