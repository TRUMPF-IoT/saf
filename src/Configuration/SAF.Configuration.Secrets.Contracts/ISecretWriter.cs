// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Contracts;
/// <summary>
/// Writes and removes secrets in a secure store. Split from <see cref="ISecretReader"/> so that
/// components which only provision secrets (e.g. a product installer) depend on the narrowest
/// possible abstraction.
/// </summary>
public interface ISecretWriter
{
    /// <summary>
    /// Stores <paramref name="value"/> under <paramref name="name"/>, overwriting any existing secret.
    /// </summary>
    /// <param name="name">The logical secret name (not the raw store key).</param>
    /// <param name="value">The secret value to store.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the secret stored under <paramref name="name"/>. Succeeds even if it does not exist.
    /// </summary>
    /// <param name="name">The logical secret name (not the raw store key).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task RemoveSecretAsync(string name, CancellationToken cancellationToken = default);
}
