// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Configuration;

using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// An <see cref="ISecretReferencePolicy"/> that lets each listed configuration key refer to the secrets listed
/// for it and refuses every other reference. Keys match case-insensitively, as configuration keys do; names
/// match the way the store looks them up (see <see cref="SecretTargetName"/>).
/// </summary>
public sealed class SecretReferenceAllowList : ISecretReferencePolicy
{
    private readonly Dictionary<string, HashSet<string>> _names = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="references">
    /// The allowed references: each configuration key with the logical name of a secret it may refer to. A key
    /// may appear more than once.
    /// </param>
    /// <exception cref="ArgumentException">A key or a name is null, empty or whitespace.</exception>
    public SecretReferenceAllowList(IEnumerable<KeyValuePair<string, string>> references)
    {
        ArgumentNullException.ThrowIfNull(references);

        foreach (var (key, name) in references)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    $"Every allowed secret reference needs a configuration key and a secret name, but got '{key}' and '{name}'.",
                    nameof(references));
            }

            if (!_names.TryGetValue(key, out var names))
            {
                names = new HashSet<string>(StringComparer.Ordinal);
                _names.Add(key, names);
            }

            names.Add(Normalize(name));
        }
    }

    /// <inheritdoc/>
    public bool Allows(string key, SecretReference reference)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(reference);

        return _names.TryGetValue(key, out var names) && names.Contains(Normalize(reference.Name));
    }

    private static string Normalize(string name) => SecretTargetName.Build(ns: null, name);
}
