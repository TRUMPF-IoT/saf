// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.FileStore;

using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// A cross-platform <see cref="ISecretStoreProvider"/> that persists secrets to a single JSON file.
/// Each value is encrypted at rest through an injected <see cref="ISecretProtector"/> (PKCS#7/CMS by
/// default); the logical names remain in clear, matching the security model that a secret reference is
/// not itself sensitive. File permissions (0600 on Linux, an NTFS ACL for the configured reader on
/// Windows) are intentionally the responsibility of the installer/deployment, not of this provider.
/// </summary>
internal sealed class FileSecretStore : ISecretStoreProvider, IDisposable
{
    /// <summary>The stable provider name used for explicit selection.</summary>
    public const string ProviderName = "file";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    // Retry interval while waiting for another process to release the cross-process lock (see
    // AcquireCrossProcessLockAsync). FileShare.None fails a contended Open immediately rather than
    // queuing it, so the wait is implemented as a poll loop instead of a blocking OS wait.
    private static readonly TimeSpan CrossProcessLockRetryDelay = TimeSpan.FromMilliseconds(25);

    private readonly IFileSystem _fileSystem;
    private readonly ISecretProtector? _protector;
    private readonly SecretStoreOptions _options;
    private readonly FileSecretStoreOptions _fileOptions;
    private readonly ILogger<FileSecretStore> _logger;

    // Serializes all file access within this process. A cross-process lock (see
    // AcquireCrossProcessLockAsync) additionally serializes against other processes, e.g. an installer
    // or CLI tool writing to the same store file concurrently with this one.
    private readonly SemaphoreSlim _fileGate = new(1, 1);

    public FileSecretStore(
        IFileSystem fileSystem,
        IOptions<SecretStoreOptions> options,
        IOptions<FileSecretStoreOptions> fileOptions,
        ILogger<FileSecretStore> logger,
        ISecretProtector? protector = null)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fileOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _fileSystem = fileSystem;
        // The protector is optional: without it the store cannot encrypt at rest, so it reports
        // IsAvailable=false and auto-selection skips it, rather than failing to construct. This keeps
        // AddDefaults() usable on non-Windows even before a consumer registers an ISecretProtector.
        _protector = protector;
        _options = options.Value;
        _fileOptions = fileOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => ProviderName;

    /// <inheritdoc />
    public bool IsAvailable
    {
        get
        {
            if (_protector is not null)
            {
                return true;
            }

            _logger.LogWarning(
                "The file secret store is registered but no {Protector} is available, so it cannot " +
                "protect secrets at rest and is treated as unavailable. Register one, for example " +
                "services.AddSingleton<ISecretProtector>(_ => new PkcsSecretProtector(certificate)).",
                nameof(ISecretProtector));
            return false;
        }
    }

    // Guards every crypto and file operation: the store must never be used without a protector. Auto- and
    // named selection already gate on IsAvailable, so this only trips when the store is used directly.
    private ISecretProtector Protector => _protector
        ?? throw new InvalidOperationException(
            $"The file secret store has no {nameof(ISecretProtector)} configured. Register one before use, " +
            "for example services.AddSingleton<ISecretProtector>(_ => new PkcsSecretProtector(certificate)).");

    /// <inheritdoc />
    public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        cancellationToken.ThrowIfCancellationRequested();

        await _fileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var crossProcessLock = await AcquireCrossProcessLockAsync(cancellationToken).ConfigureAwait(false);

            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            return document.Secrets.TryGetValue(BuildTargetName(name), out var encoded)
                ? Decrypt(encoded)
                : null;
        }
        finally
        {
            _fileGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();

        await _fileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var crossProcessLock = await AcquireCrossProcessLockAsync(cancellationToken).ConfigureAwait(false);

            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            document.Secrets[BuildTargetName(name)] = Encrypt(value);
            await WriteDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _fileGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task RemoveSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        cancellationToken.ThrowIfCancellationRequested();

        await _fileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var crossProcessLock = await AcquireCrossProcessLockAsync(cancellationToken).ConfigureAwait(false);

            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            if (document.Secrets.Remove(BuildTargetName(name)))
            {
                await WriteDocumentAsync(document, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _fileGate.Release();
        }
    }

    // Serializes access to the store file across processes (e.g. this host and an installer/CLI tool
    // writing to the same file). FileShare.None fails immediately rather than queuing, so a contended
    // open is retried until the holder releases it or the caller cancels.
    private async Task<Stream> AcquireCrossProcessLockAsync(CancellationToken cancellationToken)
    {
        var path = ResolvePath();
        var directory = _fileSystem.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            _fileSystem.Directory.CreateDirectory(directory);
        }

        var lockPath = path + ".lock";
        while (true)
        {
            try
            {
                return _fileSystem.File.Open(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                await Task.Delay(CrossProcessLockRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _fileGate.Dispose();

    private string Encrypt(string value)
    {
        var plaintext = Encoding.UTF8.GetBytes(value);
        try
        {
            return Convert.ToBase64String(Protector.Protect(plaintext));
        }
        finally
        {
            // Remove the plaintext copy from managed memory once it has been enveloped.
            Array.Clear(plaintext);
        }
    }

    private string Decrypt(string encoded)
    {
        var plaintext = Protector.Unprotect(Convert.FromBase64String(encoded));
        try
        {
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            Array.Clear(plaintext);
        }
    }

    private async Task<SecretDocument> ReadDocumentAsync(CancellationToken cancellationToken)
    {
        var path = ResolvePath();
        if (!_fileSystem.File.Exists(path))
        {
            return new SecretDocument { Protector = Protector.Name };
        }

        var json = await _fileSystem.File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        var document = JsonSerializer.Deserialize<SecretDocument>(json, JsonOptions)
            ?? new SecretDocument { Protector = Protector.Name };

        if (!string.IsNullOrEmpty(document.Protector)
            && !string.Equals(document.Protector, Protector.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The secret store file '{path}' was written by protector '{document.Protector}', but the " +
                $"configured protector is '{Protector.Name}'. Its secrets cannot be decrypted with the current configuration.");
        }

        return document;
    }

    private async Task WriteDocumentAsync(SecretDocument document, CancellationToken cancellationToken)
    {
        var path = ResolvePath();
        var directory = _fileSystem.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            _fileSystem.Directory.CreateDirectory(directory);
        }

        document.Protector = Protector.Name;
        var json = JsonSerializer.Serialize(document, JsonOptions);

        // A randomized sibling name: a fixed "<path>.tmp" collides across concurrent writers and is a
        // predictable target if the directory is writable by more than intended.
        var tempPath = _fileSystem.Path.Combine(
            string.IsNullOrEmpty(directory) ? string.Empty : directory,
            $"{_fileSystem.Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await _fileSystem.File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
            PreserveExistingPermissions(path, tempPath);
            ReplaceAtomically(tempPath, path);
        }
        catch
        {
            if (_fileSystem.File.Exists(tempPath))
            {
                _fileSystem.File.Delete(tempPath);
            }

            throw;
        }
    }

    // Move()/rename() keep the SOURCE file's permissions, so a freshly written temp file would silently
    // widen an existing store file's access on every write. Restrict the temp file first, then adopt the
    // destination's actual mode if it already exists, preserving whatever the installer configured.
    private void PreserveExistingPermissions(string path, string tempPath)
    {
        if (OperatingSystem.IsWindows())
        {
            // The Windows ACL is preserved by ReplaceAtomically itself (File.Replace keeps the
            // destination's security descriptor; File.Move does not - confirmed empirically).
            return;
        }

        _fileSystem.File.SetUnixFileMode(tempPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        if (_fileSystem.File.Exists(path))
        {
            _fileSystem.File.SetUnixFileMode(tempPath, _fileSystem.File.GetUnixFileMode(path));
        }
    }

    private void ReplaceAtomically(string tempPath, string path)
    {
        if (OperatingSystem.IsWindows() && _fileSystem.File.Exists(path))
        {
            // File.Replace preserves the destination's ACL; File.Move does not (confirmed empirically
            // against real NTFS). File.Replace requires the destination to already exist.
            _fileSystem.File.Replace(tempPath, path, destinationBackupFileName: null);
            return;
        }

        _fileSystem.File.Move(tempPath, path, overwrite: true);
    }

    private string ResolvePath()
    {
        if (!string.IsNullOrWhiteSpace(_fileOptions.Path))
        {
            return _fileOptions.Path;
        }

        var ns = string.IsNullOrEmpty(_options.Namespace) ? "saf" : _options.Namespace;
        return _fileSystem.Path.Combine(AppContext.BaseDirectory, "secrets", $"{ns}.secrets.json");
    }

    private string BuildTargetName(string name)
    {
        var ns = _options.Namespace;
        return string.IsNullOrEmpty(ns) ? name : $"{ns}/{name}";
    }

    /// <summary>The on-disk shape of the secret store file: encrypted values keyed by namespaced name.</summary>
    private sealed class SecretDocument
    {
        /// <summary>The name of the protector that enveloped the values, stamped for read-time validation.</summary>
        public string? Protector { get; set; }

        /// <summary>Base64-encoded protected payloads keyed by the namespaced target name.</summary>
        public Dictionary<string, string> Secrets { get; set; } = new(StringComparer.Ordinal);
    }
}
