// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Contracts;
/// <summary>
/// Reads secrets from a secure store. Split from <see cref="ISecretWriter"/> so read-only
/// consumers (e.g. configuration resolution) depend on the narrowest possible abstraction.
/// </summary>
public interface ISecretReader
{
    /// <summary>
    /// Returns the secret stored under <paramref name="name"/>, or <see langword="null"/> if it does not exist.
    /// </summary>
    /// <param name="name">The logical secret name (not the raw store key).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The secret value, or <see langword="null"/> when not found.</returns>
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default);
}
