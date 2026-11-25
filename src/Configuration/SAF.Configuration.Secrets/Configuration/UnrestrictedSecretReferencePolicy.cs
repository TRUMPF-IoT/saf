// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Configuration;

using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// The policy that applies when none is registered: every key may refer to every secret.
/// </summary>
internal sealed class UnrestrictedSecretReferencePolicy : ISecretReferencePolicy
{
    public bool Allows(string key, SecretReference reference) => true;
}
