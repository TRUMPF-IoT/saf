// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.WindowsCredentialManager;

/// <summary>
/// A thin, managed abstraction over the native Windows Credential Manager API
/// (<c>advapi32.dll</c> <c>CredRead</c>/<c>CredWrite</c>/<c>CredDelete</c>). Kept behind an interface
/// so the credential store logic can be unit tested without invoking the platform, mirroring how the
/// Authenticode subsystem hides its P/Invoke behind an interface.
/// </summary>
internal interface INativeCredentialApi
{
    /// <summary>
    /// Reads a generic credential by target name.
    /// </summary>
    /// <param name="targetName">The credential target name (the raw store key).</param>
    /// <param name="secret">The stored secret when found; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the credential exists; otherwise <see langword="false"/>.</returns>
    bool TryReadGenericCredential(string targetName, out string? secret);

    /// <summary>
    /// Creates or overwrites a generic credential.
    /// </summary>
    /// <param name="targetName">The credential target name (the raw store key).</param>
    /// <param name="secret">The secret value to store.</param>
    void WriteGenericCredential(string targetName, string secret);

    /// <summary>
    /// Deletes a generic credential by target name.
    /// </summary>
    /// <param name="targetName">The credential target name (the raw store key).</param>
    /// <returns><see langword="true"/> when a credential was deleted; <see langword="false"/> when none existed.</returns>
    bool DeleteGenericCredential(string targetName);
}
