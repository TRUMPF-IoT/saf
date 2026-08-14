// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets;

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
/// secret. Values that are not references are left untouched. An environment variable derived from the
/// reference name takes precedence over the store (for provisioning in CI/containers).
/// </summary>
/// <remarks>
/// When a host <see cref="IServiceProvider"/> is available (the SAF plugin-system integration, via
/// <see cref="PluginConfigurationSourceContext.HostServices"/>), the reader and options are resolved from
/// it directly. The standalone <c>AddResolvedSecrets(IConfigurationBuilder, ...)</c> overload has no host
/// container, so it builds a small self-contained one from <c>configure</c>/<c>configureProviders</c> instead.
/// </remarks>
internal sealed class SecretResolvingConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly IConfigurationRoot _inner;
    private readonly SecretStoreOptions _options;
    private readonly Lazy<ISecretReader> _reader;
    private readonly ILogger _logger;
    private readonly IDisposable? _innerReloadRegistration;
    private ServiceProvider? _standaloneServices;

    public SecretResolvingConfigurationProvider(
        IEnumerable<IConfigurationSource> innerSources,
        Action<SecretStoreOptions>? configure,
        Action<ISecretStoreBuilder>? configureProviders,
        IServiceProvider? hostServices)
    {
        ArgumentNullException.ThrowIfNull(innerSources);

        if (hostServices is not null)
        {
            _options = hostServices.GetRequiredService<IOptions<SecretStoreOptions>>().Value;
            _reader = new Lazy<ISecretReader>(() => hostServices.GetRequiredService<ISecretStore>());
            _logger = hostServices.GetRequiredService<ILogger<SecretResolvingConfigurationProvider>>();
        }
        else
        {
            // No host container (the standalone AddResolvedSecrets(IConfigurationBuilder, ...) overload):
            // options are needed even before a reader is built, so derive them directly from the callback.
            _options = new SecretStoreOptions();
            configure?.Invoke(_options);
            _reader = new Lazy<ISecretReader>(() => BuildStandaloneReader(configure, configureProviders));
            // No host ILoggerFactory to attach to either; the standalone overload never logged anything before.
            _logger = NullLogger<SecretResolvingConfigurationProvider>.Instance;
        }

        var innerBuilder = new ConfigurationBuilder();
        foreach (var source in innerSources)
        {
            innerBuilder.Add(source);
        }

        _inner = innerBuilder.Build();
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
            return;
        }

        var changed = !DataEquals(Data, newData);
        Data = newData;
        if (changed)
        {
            OnReload();
        }
    }

    private Dictionary<string, string?> BuildData()
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _inner.AsEnumerable())
        {
            if (pair.Value is not null && SecretReference.TryParse(pair.Value, _options.ReferencePrefix, out var reference))
            {
                data[pair.Key] = Resolve(reference);
            }
        }

        return data;
    }

    private string? Resolve(SecretReference reference)
    {
        if (_options.AllowEnvironmentOverride)
        {
            var overrideValue = Environment.GetEnvironmentVariable(BuildEnvironmentVariableName(reference.Name));
            if (overrideValue is not null)
            {
                return overrideValue;
            }
        }

        var value = _reader.Value.GetSecretAsync(reference.Name).GetAwaiter().GetResult();
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

        _standaloneServices = services.BuildServiceProvider();
        return _standaloneServices.GetRequiredService<ISecretStore>();
    }

    private string BuildEnvironmentVariableName(string name)
    {
        var builder = new StringBuilder(_options.EnvironmentVariablePrefix).Append("__");
        foreach (var character in name)
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
        (_inner as IDisposable)?.Dispose();
        _standaloneServices?.Dispose();
    }
}
