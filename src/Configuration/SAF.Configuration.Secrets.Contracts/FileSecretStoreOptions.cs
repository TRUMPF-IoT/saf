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

    /// <summary>
    /// Upper bound on waiting for another process to release its exclusive hold on the store file.
    /// The store file itself is the cross-process lock, so a foreign holder - an installer, a backup
    /// agent, an on-access scanner - makes every secret operation in this process wait. Defaults to
    /// 30 seconds, after which the operation throws a <see cref="TimeoutException"/>;
    /// <see cref="Timeout.InfiniteTimeSpan"/> waits forever.
    /// </summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
