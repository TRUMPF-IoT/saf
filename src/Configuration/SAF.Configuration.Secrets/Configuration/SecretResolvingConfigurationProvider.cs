// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Configuration;

using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// A configuration provider that replaces secret references (values starting with
/// <see cref="SecretStoreOptions.ReferencePrefix"/>) in the underlying configuration with the resolved
/// secret. Values that are not references are left untouched. When
/// <see cref="SecretStoreOptions.AllowEnvironmentOverride"/> is enabled, an environment variable derived
/// from the store key takes precedence over the store (for provisioning in CI/containers).
/// </summary>
/// <remarks>
/// When a host <see cref="IServiceProvider"/> is available (the SAF plugin-system integration, via
/// <see cref="PluginConfigurationSourceContext.HostServices"/>), the reader and options are resolved from
/// it directly. The standalone <c>AddResolvedSecrets(IConfigurationBuilder, ...)</c> overload has no host
/// container, so it builds a small self-contained one from <c>configure</c>/<c>configureProviders</c> instead.
/// </remarks>
internal sealed class SecretResolvingConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly IConfiguration _inner;
    private readonly IReadOnlyList<IConfigurationProvider> _shadowingProviders;
    private readonly bool _isChainedRoot;
    private readonly SecretStoreOptions _options;
    // PublicationOnly: the default mode caches a factory exception, latching a transient failure - a
    // custom provider's constructor, a shutdown race - for the process lifetime.
    private readonly Lazy<ISecretReader> _reader;
    private readonly ILogger _logger;
    private readonly IDisposable? _innerReloadRegistration;
    private ServiceProvider? _standaloneServices;

    /// <param name="inner">The configuration whose secret references are resolved.</param>
    /// <param name="shadowingProviders">
    /// The providers of <paramref name="inner"/> that come after this one in the configuration this
    /// provider is part of. A reference supplied by one of them cannot be overridden here, so it is
    /// reported instead of being passed through as its literal token.
    /// </param>
    /// <param name="isChainedRoot">
    /// <see langword="true"/> when <paramref name="inner"/> is chained into the same configuration as this
    /// provider and owned by that chain, <see langword="false"/> when it was built by (and belongs to) the
    /// source that created this provider. It also decides who reports a reload: a chained inner root is
    /// muted, so this provider raises the notification for every change, not only for changed secrets.
    /// </param>
    public SecretResolvingConfigurationProvider(
        IConfiguration inner,
        IReadOnlyList<IConfigurationProvider> shadowingProviders,
        bool isChainedRoot,
        Action<SecretStoreOptions>? configure,
        Action<ISecretStoreBuilder>? configureProviders,
        IServiceProvider? hostServices)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(shadowingProviders);

        if (hostServices is not null)
        {
            _options = hostServices.GetRequiredService<IOptions<SecretStoreOptions>>().Value;
            _reader = new Lazy<ISecretReader>(
                () => hostServices.GetRequiredService<ISecretStore>(), LazyThreadSafetyMode.PublicationOnly);
            _logger = hostServices.GetRequiredService<ILogger<SecretResolvingConfigurationProvider>>();
        }
        else
        {
            // No host container (the standalone AddResolvedSecrets(IConfigurationBuilder, ...) overload):
            // options are needed even before a reader is built, so derive them directly from the callback.
            _options = new SecretStoreOptions();
            configure?.Invoke(_options);
            _reader = new Lazy<ISecretReader>(
                () => BuildStandaloneReader(configure, configureProviders), LazyThreadSafetyMode.PublicationOnly);
            // No host ILoggerFactory to attach to either; the standalone overload never logged anything before.
            _logger = NullLogger<SecretResolvingConfigurationProvider>.Instance;
        }

        if (_options.ResolveTimeout <= TimeSpan.Zero && _options.ResolveTimeout != Timeout.InfiniteTimeSpan)
        {
            throw new InvalidOperationException(
                $"'{_options.ResolveTimeout}' cannot be used as {nameof(SecretStoreOptions.ResolveTimeout)}: it " +
                $"must be positive, or {nameof(Timeout)}.{nameof(Timeout.InfiniteTimeSpan)} to wait indefinitely.");
        }

        _inner = inner;
        _shadowingProviders = shadowingProviders;
        _isChainedRoot = isChainedRoot;
        _innerReloadRegistration = ChangeToken.OnChange(() => _inner.GetReloadToken(), Reload);
    }

    // Initial Load() intentionally lets a build-up exception (an unresolved reference, or the store
    // itself throwing) propagate: the host should fail fast rather than start with a missing credential.
    public override void Load() => Data = BuildData();

    // Unlike Load(), a failure here must never reach the ChangeToken/file-watcher callback thread that
    // invokes it, or it takes the whole host down over what may be a transient store/reload failure.
    // Keep serving the last-known-good Data instead.
    private void Reload()
    {
        Dictionary<string, string?> newData;
        try
        {
            newData = BuildData();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reload secret-resolved configuration; keeping the previously resolved values.");

            // A chained inner root is muted, so its own changes reach consumers only through this
            // notification. Withholding it because the secrets could not be re-resolved would hide the
            // non-secret part of the reload as well.
            if (_isChainedRoot)
            {
                OnReload();
            }

            return;
        }

        var changed = !DataEquals(Data, newData);
        Data = newData;
        if (changed || _isChainedRoot)
        {
            OnReload();
        }
    }

    private Dictionary<string, string?> BuildData()
    {
        var references = new Dictionary<string, SecretReference>(StringComparer.Ordinal);
        var referencedKeys = new List<KeyValuePair<string, string>>();

        foreach (var pair in _inner.AsEnumerable())
        {
            if (pair.Value is not null && SecretReference.TryParse(pair.Value, _options.ReferencePrefix, out var reference))
            {
                references.TryAdd(reference.Name, reference);
                referencedKeys.Add(new KeyValuePair<string, string>(pair.Key, reference.Name));
            }
        }

        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (referencedKeys.Count == 0)
        {
            return data;
        }

        ThrowIfShadowed(referencedKeys);

        // Once per distinct reference, not once per configuration key: the same secret is routinely
        // referenced from several keys, and every resolution is a round-trip to the store.
        var resolved = ResolveAll(references.Values);
        foreach (var pair in referencedKeys)
        {
            data[pair.Key] = resolved[pair.Value];
        }

        return data;
    }

    // A reference that the configuration takes from a provider placed after this one cannot be replaced
    // here - that provider answers TryGet first, and the consumer would receive the literal secret://
    // token as its credential. Report it instead: silently handing out an unresolved reference is the
    // fail-open behaviour this provider exists to prevent.
    private void ThrowIfShadowed(List<KeyValuePair<string, string>> referencedKeys)
    {
        if (_shadowingProviders.Count == 0)
        {
            return;
        }

        var shadowed = referencedKeys
            .Where(pair => _shadowingProviders.Any(provider => provider.TryGet(pair.Key, out _)))
            .Select(pair => pair.Key)
            .ToList();

        if (shadowed.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"The secret reference(s) at {string.Join(", ", shadowed.Select(key => $"'{key}'"))} come from a " +
            "configuration source that was added after secret resolution, so the unresolved reference would " +
            "win over the resolved value. Add that source before the AddResolvedSecrets call.");
    }

    private Dictionary<string, string?> ResolveAll(ICollection<SecretReference> references)
    {
        var timeout = new CancellationTokenSource();

        // Task.Run keeps the one blocking wait off any captured SynchronizationContext: Load() is
        // synchronous by contract, and a store whose continuations resume on the calling context
        // would otherwise deadlock instead of answering.
        var work = Task.Run(() => ResolveAllAsync(references, timeout.Token), CancellationToken.None);

        var abandoned = false;
        try
        {
            // WaitAsync bounds the wait itself instead of relying on the token: a provider that blocks
            // inside a synchronous call - the in-box Windows one blocks in CredReadW - never observes
            // cancellation, so waiting for the token to come back is waiting forever.
            return work.WaitAsync(ResolveWait).GetAwaiter().GetResult();
        }
        catch (TimeoutException) when (!work.IsCompleted)
        {
            // Cancelling stays a courtesy to a cooperative provider; the wait is over either way.
            abandoned = true;
            timeout.Cancel();
            Abandon(work, timeout);

            throw new TimeoutException(
                $"Resolving {references.Count} secret reference(s) did not complete within {_options.ResolveTimeout}. " +
                $"Raise {nameof(SecretStoreOptions)}.{nameof(SecretStoreOptions.ResolveTimeout)} or check that the " +
                $"'{_options.ProviderName}' secret store provider is reachable.");
        }
        finally
        {
            if (!abandoned)
            {
                timeout.Dispose();
            }
        }
    }

    // Clamped because WaitAsync rejects anything above int.MaxValue milliseconds; a timeout of 24 days
    // is indistinguishable from waiting forever anyway.
    private TimeSpan ResolveWait => _options.ResolveTimeout == Timeout.InfiniteTimeSpan
        ? Timeout.InfiniteTimeSpan
        : TimeSpan.FromMilliseconds(Math.Min(_options.ResolveTimeout.TotalMilliseconds, int.MaxValue));

    // The abandoned resolve keeps running and still holds the token, so the source cannot be disposed
    // here; the continuation also observes its exception, which would otherwise go unobserved.
    private static void Abandon(Task task, CancellationTokenSource timeout)
        => _ = task.ContinueWith(
            completed =>
            {
                _ = completed.Exception;
                timeout.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private async Task<Dictionary<string, string?>> ResolveAllAsync(
        ICollection<SecretReference> references,
        CancellationToken cancellationToken)
    {
        var resolved = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var reference in references)
        {
            resolved[reference.Name] = await ResolveAsync(reference, cancellationToken).ConfigureAwait(false);
        }

        return resolved;
    }

    private async Task<string?> ResolveAsync(SecretReference reference, CancellationToken cancellationToken)
    {
        if (_options.AllowEnvironmentOverride)
        {
            var variableName = BuildEnvironmentVariableName(reference.Name);
            var overrideValue = Environment.GetEnvironmentVariable(variableName);
            if (overrideValue is not null)
            {
                _logger.LogDebug(
                    "Secret reference '{Reference}' resolved from environment variable '{Variable}' instead of the store.",
                    reference,
                    variableName);
                return overrideValue;
            }
        }

        var value = await _reader.Value.GetSecretAsync(reference.Name, cancellationToken).ConfigureAwait(false);
        if (value is null && _options.ThrowOnUnresolvedReference)
        {
            throw new InvalidOperationException(
                $"The secret reference '{reference}' could not be resolved: no value named '{reference.Name}' " +
                $"was found by the '{_options.ProviderName}' secret store provider. Set " +
                $"{nameof(SecretStoreOptions.ThrowOnUnresolvedReference)} = false to pass unresolved " +
                "references through as null instead.");
        }

        return value;
    }

    private ISecretReader BuildStandaloneReader(
        Action<SecretStoreOptions>? configure,
        Action<ISecretStoreBuilder>? configureProviders)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var storeBuilder = services.AddSecretStore(configure);
        if (configureProviders is null)
        {
            storeBuilder.AddDefaults();
        }
        else
        {
            configureProviders(storeBuilder);
        }

        var built = services.BuildServiceProvider();

        // PublicationOnly lets several callers run this factory but keeps one value, and it disposes
        // none of them: without this, every loser's container would leak.
        var kept = Interlocked.CompareExchange(ref _standaloneServices, built, null) ?? built;
        if (!ReferenceEquals(kept, built))
        {
            built.Dispose();
        }

        return kept.GetRequiredService<ISecretStore>();
    }

    private string BuildEnvironmentVariableName(string name)
    {
        var prefix = _options.EnvironmentVariablePrefix;
        if (string.IsNullOrWhiteSpace(prefix) || !prefix.All(c => char.IsLetterOrDigit(c) || c == '_'))
        {
            throw new InvalidOperationException(
                $"'{prefix}' cannot be used as {nameof(SecretStoreOptions.EnvironmentVariablePrefix)}: it " +
                "must be non-empty and contain only letters, digits and underscores, otherwise the derived " +
                "variable name can never be set.");
        }

        // Derived from the namespaced store key, not the bare reference name, so two hosts sharing an
        // environment but using different namespaces do not share one override variable.
        var builder = new StringBuilder(prefix).Append("__");
        foreach (var character in SecretTargetName.Build(_options.Namespace, name))
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (character == '/')
            {
                builder.Append("__");
            }
            else
            {
                builder.Append('_');
            }
        }

        return builder.ToString();
    }

    private static bool DataEquals(IDictionary<string, string?> left, IDictionary<string, string?> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var value) || value != pair.Value)
            {
                return false;
            }
        }

        return true;
    }

    public void Dispose()
    {
        _innerReloadRegistration?.Dispose();

        // A chained inner root is owned by the chain it was added to, not by this provider.
        if (!_isChainedRoot)
        {
            (_inner as IDisposable)?.Dispose();
        }

        _standaloneServices?.Dispose();
    }
}
