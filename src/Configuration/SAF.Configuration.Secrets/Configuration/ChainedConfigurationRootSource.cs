// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

/// <summary>
/// Chains an already built <see cref="IConfigurationRoot"/> into another configuration, like
/// <see cref="ChainedConfigurationSource"/> but without turning a configured empty value into a missing one.
/// </summary>
/// <remarks>
/// The in-box <c>ChainedConfigurationProvider.TryGet</c> reads the chained configuration through its
/// indexer and reports <see langword="false"/> for <see cref="string.IsNullOrEmpty"/>, so every
/// deliberately blank setting reads as <see langword="null"/> once chained. Reading the inner providers
/// directly keeps "not configured" and "configured as empty" apart, which is what enabling secret
/// resolution must not change about unrelated settings.
/// </remarks>
internal sealed class ChainedConfigurationRootSource(IConfigurationRoot root, bool shouldDisposeRoot) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
        => new ChainedConfigurationRootProvider(root, shouldDisposeRoot);

    private sealed class ChainedConfigurationRootProvider : IConfigurationProvider, IDisposable
    {
        private readonly IConfigurationRoot _root;
        private readonly bool _shouldDisposeRoot;

        // Fixed once the root is built, so it is snapshotted rather than re-enumerated per lookup.
        private readonly IConfigurationProvider[] _providers;

        public ChainedConfigurationRootProvider(IConfigurationRoot root, bool shouldDisposeRoot)
        {
            _root = root;
            _shouldDisposeRoot = shouldDisposeRoot;
            _providers = root.Providers.ToArray();
        }

        // Reverse order, matching ConfigurationRoot's own "the last provider that has the key wins".
        public bool TryGet(string key, out string? value)
        {
            for (var i = _providers.Length - 1; i >= 0; i--)
            {
                if (_providers[i].TryGet(key, out value))
                {
                    return true;
                }
            }

            value = null;
            return false;
        }

        public void Set(string key, string? value) => _root[key] = value;

        public IChangeToken GetReloadToken() => _root.GetReloadToken();

        public void Load()
        {
        }

        public IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string? parentPath)
        {
            var section = parentPath is null ? (IConfiguration)_root : _root.GetSection(parentPath);
            var keys = section.GetChildren().Select(child => child.Key).ToList();
            keys.AddRange(earlierKeys);
            keys.Sort(ConfigurationKeyComparer.Instance);
            return keys;
        }

        public void Dispose()
        {
            if (_shouldDisposeRoot)
            {
                (_root as IDisposable)?.Dispose();
            }
        }
    }
}
