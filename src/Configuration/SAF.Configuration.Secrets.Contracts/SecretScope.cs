// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Contracts;
/// <summary>
/// The isolation scope of a stored secret — i.e. which security principals are able to read it.
/// This is the isolation axis only; the concrete running identity is arbitrary (LocalSystem,
/// NetworkService, a virtual <c>NT SERVICE\*</c> account, a gMSA, or a local/domain user).
/// </summary>
public enum SecretScope
{
    /// <summary>
    /// The secret is bound to a single security principal and only that principal can decrypt it
    /// (e.g. the Credential Manager vault of the running identity, or DPAPI current-user encryption
    /// combined with a file ACL for <see cref="FileSecretStoreOptions.ReaderPrincipal"/>). Strong isolation.
    /// </summary>
    ServiceAccount,

    /// <summary>
    /// Any local account/process on the machine can decrypt the secret (e.g. DPAPI local-machine
    /// encryption). Weaker isolation, but writable by an installer without impersonation.
    /// </summary>
    Machine
}
