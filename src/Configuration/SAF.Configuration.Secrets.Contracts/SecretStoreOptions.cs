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
    /// Upper bound on resolving all secret references of one configuration load. Configuration providers
    /// load synchronously, so a store that never answers would otherwise hang host startup with no
    /// diagnostic at all. Defaults to 30 seconds; <see cref="Timeout.InfiniteTimeSpan"/> waits forever.
    /// </summary>
    public TimeSpan ResolveTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// When <see langword="true"/>, secret resolution first checks an environment variable derived from
    /// the store key (see <see cref="EnvironmentVariablePrefix"/>) before querying the store, which
    /// allows provisioning in CI/containers without an OS store.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="false"/>: enabling it makes the process environment a trust boundary
    /// for every secret, because anyone able to set that environment can substitute any value without
    /// touching the store at all. Turn it on deliberately, for CI and development.
    /// </remarks>
    public bool AllowEnvironmentOverride { get; set; }

    /// <summary>
    /// Prefix of the environment variable checked when <see cref="AllowEnvironmentOverride"/> is enabled.
    /// The lower-cased store key is appended — <see cref="Namespace"/> included — with <c>/</c> replaced
    /// by <c>__</c> and every other non-alphanumeric character by <c>_</c>; e.g.
    /// <c>secret://conn-1/password</c> under namespace <c>myproduct</c> maps to
    /// <c>SECRET__myproduct__conn_1__password</c>.
    /// </summary>
    /// <remarks>
    /// That derivation is deliberately readable rather than injective, so two names differing only in
    /// their non-alphanumeric characters share one variable (<c>a/b</c> collides with <c>a--b</c>,
    /// <c>a-b</c> with <c>a.b</c>). See "Environment overrides" in docs/secret-store.md.
    /// </remarks>
    public string EnvironmentVariablePrefix { get; set; } = "SECRET";
}
