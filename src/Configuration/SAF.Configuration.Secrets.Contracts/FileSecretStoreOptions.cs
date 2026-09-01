// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Contracts;
/// <summary>
/// Options specific to the file-based secret store provider. Ignored by the Credential Manager and
/// systemd providers, which keep secrets outside the filesystem.
/// </summary>
public sealed class FileSecretStoreOptions
{
    /// <summary>
    /// Filesystem path of the secret store file. When not set a provider-specific default is used.
    /// </summary>
    public string? Path { get; set; }
}
