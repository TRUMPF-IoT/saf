// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Configuration;

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

/// <summary>
/// Delegates to an inner <see cref="IConfigurationRoot"/> but never signals a reload. Used to chain the
/// inner root into the resolving configuration: the resolving provider raises the one reload notification,
/// after it has re-resolved. Forwarding the inner token as well would notify consumers first - callbacks
/// on a reload token run last-registered-first - and a reference added by that very reload would read as
/// its literal <c>secret://</c> token until the resolver caught up.
/// </summary>
internal sealed class UnwatchedConfigurationRoot(IConfigurationRoot inner) : IConfigurationRoot, IDisposable
{
    public string? this[string key]
    {
        get => inner[key];
        set => inner[key] = value;
    }

    public IEnumerable<IConfigurationProvider> Providers => inner.Providers;

    public IEnumerable<IConfigurationSection> GetChildren() => inner.GetChildren();

    public IConfigurationSection GetSection(string key) => inner.GetSection(key);

    public IChangeToken GetReloadToken() => NeverChangeToken.Instance;

    public void Reload() => inner.Reload();

    public void Dispose() => (inner as IDisposable)?.Dispose();

    private sealed class NeverChangeToken : IChangeToken
    {
        public static readonly NeverChangeToken Instance = new();

        public bool HasChanged => false;

        public bool ActiveChangeCallbacks => false;

        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => NullDisposable.Instance;

        private sealed class NullDisposable : IDisposable
        {
            [SuppressMessage("Minor Code Smell", "S3218", Justification = "Using 'Instance' for singleton-style fields is intentional.")]
            public static readonly NullDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
