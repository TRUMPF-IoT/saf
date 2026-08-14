// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Contracts;
/// <summary>
/// Configuration for the secret store and its transparent configuration resolution.
/// </summary>
public sealed class SecretStoreOptions
{
    /// <summary>
    /// The value of <see cref="ProviderName"/> that selects a provider automatically by platform
    /// and availability instead of by an explicit name.
    /// </summary>
    public const string AutoProviderName = "auto";

    /// <summary>
    /// Name of the <see cref="ISecretStoreProvider"/> to use, or <see cref="AutoProviderName"/>
    /// (the default) to pick the first available provider for the current platform.
    /// </summary>
    public string ProviderName { get; set; } = AutoProviderName;

    /// <summary>
    /// The isolation scope of stored secrets. Defaults to <see cref="SecretScope.ServiceAccount"/>.
    /// </summary>
    public SecretScope Scope { get; set; } = SecretScope.ServiceAccount;

    /// <summary>
    /// The prefix that marks a configuration value as a secret reference (e.g. <c>"secret://name"</c>).
    /// Values without this prefix are passed through unchanged by the resolving configuration provider.
    /// </summary>
    public string ReferencePrefix { get; set; } = "secret://";

    /// <summary>
    /// A namespace prepended to every logical secret name to form the raw store key, keeping different
    /// products/hosts from colliding in a shared store. Defaults to <c>"saf"</c>.
    /// </summary>
    public string Namespace { get; set; } = "saf";

    /// <summary>
    /// When <see langword="true"/> (the default, intended for production), a <c>secret://</c> reference
    /// that no provider can resolve throws instead of silently becoming <see langword="null"/>. Set to
    /// <see langword="false"/> to let an unresolved reference pass through as <see langword="null"/>,
    /// e.g. in test/dev without a populated store.
    /// </summary>
    public bool ThrowOnUnresolvedReference { get; set; } = true;

    /// <summary>
    /// When <see langword="true"/> (the default), secret resolution first checks an environment
    /// variable derived from the reference name (see <see cref="EnvironmentVariablePrefix"/>) before
    /// querying the store. Enables provisioning in CI/containers without an OS store.
    /// </summary>
    public bool AllowEnvironmentOverride { get; set; } = true;

    /// <summary>
    /// Prefix of the environment variable checked for a secret override. The reference name is appended
    /// with non-alphanumeric characters replaced by <c>__</c>, e.g. reference <c>myproduct/conn-1/password</c>
    /// maps to <c>SECRET__myproduct__conn_1__password</c>.
    /// </summary>
    public string EnvironmentVariablePrefix { get; set; } = "SECRET";
}
