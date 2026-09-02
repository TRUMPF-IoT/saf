// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.WindowsCredentialManager;

using System.Text;
using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// An <see cref="ISecretStoreProvider"/> backed by the Windows Credential Manager. Secrets are stored
/// as generic credentials in the vault of the running identity, so only that principal can read them.
/// The Credential Manager has no machine-wide vault; use the file-based provider for secrets that must
/// be installer-writable and service-readable across principals.
/// </summary>
internal sealed class WindowsCredentialManagerSecretStore : ISecretStoreProvider
{
    /// <summary>The stable provider name used for explicit selection.</summary>
    public const string ProviderName = "windows-credential-manager";

    // wincred.h: the blob holds the UTF-16 secret; TargetName is also stored as UserName (see
    // WindowsCredentialManagerNativeApi), so the tighter username limit is what actually binds.
    private const int CredMaxCredentialBlobSize = 5 * 512; // CRED_MAX_CREDENTIAL_BLOB_SIZE, bytes
    private const int CredMaxUsernameLength = 513; // CRED_MAX_USERNAME_LENGTH, chars

    private readonly INativeCredentialApi _nativeApi;

    public WindowsCredentialManagerSecretStore(INativeCredentialApi nativeApi)
    {
        ArgumentNullException.ThrowIfNull(nativeApi);

        _nativeApi = nativeApi;
    }

    /// <inheritdoc />
    public string Name => ProviderName;

    /// <inheritdoc />
    public bool IsAvailable => OperatingSystem.IsWindows();

    /// <inheritdoc />
    public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        cancellationToken.ThrowIfCancellationRequested();

        var found = _nativeApi.TryReadGenericCredential(name, out var secret);
        return Task.FromResult(found ? secret : null);
    }

    /// <inheritdoc />
    public Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();

        if (name.Length > CredMaxUsernameLength)
        {
            throw new ArgumentException(
                $"The secret target name '{name}' is {name.Length} characters long, which exceeds " +
                $"the Windows Credential Manager limit of {CredMaxUsernameLength} characters " +
                "(CRED_MAX_USERNAME_LENGTH — the target name is also stored as the credential's user name). " +
                "Use a shorter name or namespace.",
                nameof(name));
        }

        var blobSize = Encoding.Unicode.GetByteCount(value);
        if (blobSize > CredMaxCredentialBlobSize)
        {
            throw new ArgumentException(
                $"The secret value for '{name}' is {blobSize} bytes when UTF-16 encoded, which exceeds " +
                $"the Windows Credential Manager limit of {CredMaxCredentialBlobSize} bytes " +
                $"(CRED_MAX_CREDENTIAL_BLOB_SIZE, ~{CredMaxCredentialBlobSize / 2} characters). Store larger " +
                "values in the file-based provider instead.",
                nameof(value));
        }

        _nativeApi.WriteGenericCredential(name, value);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        cancellationToken.ThrowIfCancellationRequested();

        _nativeApi.DeleteGenericCredential(name);
        return Task.CompletedTask;
    }
}
