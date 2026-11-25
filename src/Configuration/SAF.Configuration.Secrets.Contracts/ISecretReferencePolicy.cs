// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Contracts;

/// <summary>
/// Decides which configuration keys may refer to which secrets. Transparent configuration resolution asks it
/// about every reference it finds; a reference it refuses reads as <see langword="null"/> and is logged, and
/// the store is not read for it.
/// </summary>
public interface ISecretReferencePolicy
{
    /// <summary>
    /// Whether the value at <paramref name="key"/> may be resolved from <paramref name="reference"/>.
    /// </summary>
    /// <param name="key">The configuration key, as the configuration reports it (e.g. <c>Db:Password</c>).</param>
    /// <param name="reference">The reference found at <paramref name="key"/>.</param>
    bool Allows(string key, SecretReference reference);
}
