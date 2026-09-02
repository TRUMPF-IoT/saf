// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// The consumer-facing <see cref="ISecretStore"/> that delegates to one selected
/// <see cref="ISecretStoreProvider"/>. The active provider is chosen once from the registered
/// providers: by <see cref="SecretStoreOptions.ProviderName"/> when set explicitly, or the first
/// available provider when set to <see cref="SecretStoreOptions.AutoProviderName"/>.
/// </summary>
/// <remarks>
/// Registered providers are alternatives, not a chain. The selected one answers every call and a name
/// it does not hold is simply absent; there is no per-lookup fallback to the next provider, so reads
/// and writes always address the same backend and the answering backend is never ambiguous. Only a
/// failed selection is retried.
/// <para>
/// This is also where the logical name becomes the physical one: <see cref="SecretTargetName"/> is
/// applied here, once, so every provider - in-box or custom - is handed the same namespaced, lower-cased
/// key and none of them can lose the convention by not knowing about it.
/// </para>
/// </remarks>
internal sealed class CompositeSecretStore : ISecretStore
{
    private readonly IReadOnlyList<ISecretStoreProvider> _providers;
    private readonly SecretStoreOptions _options;
    private readonly ILogger<CompositeSecretStore> _logger;
    private readonly Lazy<ISecretStoreProvider> _activeProvider;

    public CompositeSecretStore(
        IEnumerable<ISecretStoreProvider> providers,
        IOptions<SecretStoreOptions> options,
        ILogger<CompositeSecretStore> logger)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _providers = [.. providers];
        _options = options.Value;
        _logger = logger;
        // PublicationOnly: the default mode caches the exception too, latching a failed selection for
        // the process lifetime, so a provider whose availability is a runtime fact could never recover.
        _activeProvider = new Lazy<ISecretStoreProvider>(SelectProvider, LazyThreadSafetyMode.PublicationOnly);
    }

    /// <inheritdoc />
    public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        var target = BuildTargetName(name);
        return _activeProvider.Value.GetSecretAsync(target, cancellationToken);
    }

    /// <inheritdoc />
    public Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        var target = BuildTargetName(name);
        return _activeProvider.Value.SetSecretAsync(target, value, cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        var target = BuildTargetName(name);
        return _activeProvider.Value.RemoveSecretAsync(target, cancellationToken);
    }

    // Built before the provider is selected, so an invalid name is reported as such instead of as
    // whatever selection happens to fail first. A whitespace name would otherwise reach the provider as
    // a well-formed "<namespace>/ " key and pass its own guard.
    private string BuildTargetName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return SecretTargetName.Build(_options.Namespace, name);
    }

    private ISecretStoreProvider SelectProvider()
    {
        var providerName = _options.ProviderName;

        if (!string.Equals(providerName, SecretStoreOptions.AutoProviderName, StringComparison.OrdinalIgnoreCase))
        {
            var named = _providers.FirstOrDefault(p => string.Equals(p.Name, providerName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"No secret store provider named '{providerName}' is registered. Registered providers: {DescribeProviders()}.");

            if (!named.IsAvailable)
            {
                throw new InvalidOperationException(
                    $"The secret store provider '{named.Name}' is not available in this environment.");
            }

            _logger.LogInformation("Using explicitly selected secret store provider '{Provider}'.", named.Name);
            return named;
        }

        var available = _providers.FirstOrDefault(p => p.IsAvailable)
            ?? throw new InvalidOperationException(
                $"No available secret store provider was found. Registered providers: {DescribeProviders()}.");

        _logger.LogInformation("Auto-selected secret store provider '{Provider}'.", available.Name);
        return available;
    }

    private string DescribeProviders()
        => _providers.Count == 0 ? "(none)" : string.Join(", ", _providers.Select(p => p.Name));
}
