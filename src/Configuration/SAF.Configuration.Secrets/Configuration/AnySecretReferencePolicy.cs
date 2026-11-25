// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Configuration;

using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// An <see cref="ISecretReferencePolicy"/> that allows a reference when any of its policies allows it, e.g. a
/// <see cref="KeyBoundSecretReferencePolicy"/> for most secrets and a <see cref="SecretReferenceAllowList"/> for
/// those named otherwise. Without policies it allows nothing.
/// </summary>
public sealed class AnySecretReferencePolicy : ISecretReferencePolicy
{
    private readonly ISecretReferencePolicy[] _policies;

    /// <exception cref="ArgumentException">One of the policies is null.</exception>
    public AnySecretReferencePolicy(IEnumerable<ISecretReferencePolicy> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);

        _policies = [.. policies];
        if (_policies.Any(policy => policy is null))
        {
            throw new ArgumentException("A secret reference policy cannot be null.", nameof(policies));
        }
    }

    /// <inheritdoc/>
    public bool Allows(string key, SecretReference reference)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(reference);

        return _policies.Any(policy => policy.Allows(key, reference));
    }
}
