// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.WindowsCredentialManager;

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// An <see cref="ISecretStoreProvider"/> backed by the Windows Credential Manager. Secrets are stored
/// as generic credentials in the vault of the running identity, which yields per-principal
/// (<see cref="SecretScope.ServiceAccount"/>) isolation. The Credential Manager has no machine-wide
/// vault, so <see cref="SecretScope.Machine"/> is not achievable here; use the file-based provider for
/// installer-writable, service-readable secrets.
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
    private readonly SecretStoreOptions _options;
    private readonly ILogger<WindowsCredentialManagerSecretStore> _logger;

    public WindowsCredentialManagerSecretStore(
        IOptions<SecretStoreOptions> options,
        INativeCredentialApi nativeApi,
        ILogger<WindowsCredentialManagerSecretStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(nativeApi);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _nativeApi = nativeApi;
        _logger = logger;
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

        var target = BuildTargetName(name);
        var found = _nativeApi.TryReadGenericCredential(target, out var secret);
        return Task.FromResult(found ? secret : null);
    }

    /// <inheritdoc />
    public Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();

        var target = BuildTargetName(name);
        if (target.Length > CredMaxUsernameLength)
        {
            throw new ArgumentException(
                $"The secret target name '{target}' is {target.Length} characters long, which exceeds " +
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

        if (_options.Scope == SecretScope.Machine)
        {
            _logger.LogWarning(
                "Secret scope '{Scope}' was requested but the Windows Credential Manager only provides " +
                "per-principal isolation; the secret '{Name}' is stored in the running identity's vault.",
                _options.Scope, name);
        }

        _nativeApi.WriteGenericCredential(target, value);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        cancellationToken.ThrowIfCancellationRequested();

        _nativeApi.DeleteGenericCredential(BuildTargetName(name));
        return Task.CompletedTask;
    }

    private string BuildTargetName(string name)
    {
        var ns = _options.Namespace;
        return string.IsNullOrEmpty(ns) ? name : $"{ns}/{name}";
    }
}
