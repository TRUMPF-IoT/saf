// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets;

using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
/// Configuration is built before the application's dependency injection container exists, so resolution
/// runs in two phases: until the container is available the provider resolves through a self-contained
/// bootstrap reader; once a <see cref="HostSecretStoreAccessor"/> is bound to the host container the
/// provider switches to the host's <see cref="ISecretStore"/> and re-resolves.
/// </remarks>
internal sealed class SecretResolvingConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly IConfigurationRoot _inner;
    private readonly SecretStoreOptions _options;
    private readonly HostSecretStoreAccessor? _accessor;
    private readonly Lazy<ISecretReader> _bootstrapReader;
    private readonly IDisposable? _innerReloadRegistration;
    private readonly IDisposable? _accessorReloadRegistration;
    private ServiceProvider? _bootstrapServices;

    public SecretResolvingConfigurationProvider(
        IEnumerable<IConfigurationSource> innerSources,
        Action<SecretStoreOptions>? configure,
        Action<ISecretStoreBuilder>? configureProviders,
        HostSecretStoreAccessor? accessor)
    {
        ArgumentNullException.ThrowIfNull(innerSources);

        _accessor = accessor;

        // Options (reference prefix, environment override) are needed even when no reader is ever built,
        // so derive them directly from the callback instead of from a container.
        _options = new SecretStoreOptions();
        configure?.Invoke(_options);

        _bootstrapReader = new Lazy<ISecretReader>(() => BuildBootstrapReader(configure, configureProviders));

        var innerBuilder = new ConfigurationBuilder();
        foreach (var source in innerSources)
        {
            innerBuilder.Add(source);
        }

        _inner = innerBuilder.Build();
        _innerReloadRegistration = ChangeToken.OnChange(() => _inner.GetReloadToken(), Reload);
        if (_accessor is not null)
        {
            _accessorReloadRegistration = ChangeToken.OnChange(() => _accessor.GetChangeToken(), Reload);
        }
    }

    public override void Load() => Data = BuildData();

    private void Reload()
    {
        var newData = BuildData();
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

        var reader = _accessor is not null && _accessor.TryGetReader(out var hostReader)
            ? hostReader
            : _bootstrapReader.Value;

        return reader.GetSecretAsync(reference.Name).GetAwaiter().GetResult();
    }

    private ISecretReader BuildBootstrapReader(
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

        _bootstrapServices = services.BuildServiceProvider();
        return _bootstrapServices.GetRequiredService<ISecretStore>();
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

    private static bool DataEquals(IDictionary<string, string?> left, Dictionary<string, string?> right)
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
        _accessorReloadRegistration?.Dispose();
        (_inner as IDisposable)?.Dispose();
        _bootstrapServices?.Dispose();
    }
}
