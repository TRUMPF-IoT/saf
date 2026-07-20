// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Contracts;
/// <summary>
/// Options specific to the file-based secret store provider. Ignored by the Credential Manager and
/// systemd providers (which derive the reader from the running identity or the unit).
/// </summary>
public sealed class FileSecretStoreOptions
{
    /// <summary>
    /// Filesystem path of the secret store file. When not set a provider-specific default is used.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// The principal granted exclusive read access to the store file, e.g. <c>"DOMAIN\\svc-qds"</c>
    /// or <c>"NT SERVICE\\QDS-2"</c> on Windows (NTFS ACL), or a user/group on Linux (ownership plus
    /// restrictive permissions). When not set, the file inherits the default protection for the
    /// configured <see cref="SecretScope"/>.
    /// </summary>
    public string? ReaderPrincipal { get; set; }
}
