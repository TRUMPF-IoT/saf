// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Configuration;

using Microsoft.Extensions.Configuration;
using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// An <see cref="ISecretReferencePolicy"/> that binds every secret to the key its name is spelled from: the
/// value at <c>MyPlugin:Password</c> may refer to <c>myplugin/password</c> and to nothing else. The name is the
/// key with <c>:</c> as <c>/</c>, matched the way the store looks names up (see <see cref="SecretTargetName"/>).
/// </summary>
/// <remarks>
/// A key that contains <c>/</c> may refer to no secret: <c>a/b</c> and <c>a:b</c> would share one name.
/// </remarks>
public sealed class KeyBoundSecretReferencePolicy : ISecretReferencePolicy
{
    private const char NameDelimiter = '/';

    /// <inheritdoc/>
    public bool Allows(string key, SecretReference reference)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(reference);

        return !key.Contains(NameDelimiter, StringComparison.Ordinal)
            && string.Equals(
                Normalize(key.Replace(ConfigurationPath.KeyDelimiter, NameDelimiter.ToString(), StringComparison.Ordinal)),
                Normalize(reference.Name),
                StringComparison.Ordinal);
    }

    private static string Normalize(string name) => SecretTargetName.Build(ns: null, name);
}
