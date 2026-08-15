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
/// Every write happens in place through a single handle on the store file itself - no temporary or
/// sidecar file is ever created - so the provider also works on deployment targets that only permit
/// writing an already-existing file. A crash mid-write can therefore leave the store file truncated or
/// corrupt; that is an accepted trade-off for this constraint.
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

    // Retry interval while waiting for another process to release its exclusive hold on the store file.
    // FileShare.None fails a contended Open immediately rather than queuing it, so the wait is
    // implemented as a poll loop instead of a blocking OS wait.
    private static readonly TimeSpan ExclusiveOpenRetryDelay = TimeSpan.FromMilliseconds(25);

    private readonly IFileSystem _fileSystem;
    private readonly ISecretProtector? _protector;
    private readonly SecretStoreOptions _options;
    private readonly FileSecretStoreOptions _fileOptions;
    private readonly ILogger<FileSecretStore> _logger;

    // Serializes all file access within this process. Opening the store file itself with
    // FileShare.None (see OpenExclusiveAsync/OpenIfExistsExclusiveAsync) additionally serializes
    // against other processes, e.g. an installer or CLI tool writing to the same file.
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

        var path = ResolvePath();
        await _fileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var stream = await OpenIfExistsExclusiveAsync(path, FileAccess.Read, cancellationToken).ConfigureAwait(false);
            if (stream is null)
            {
                return null;
            }

            var document = await ReadDocumentAsync(path, stream, cancellationToken).ConfigureAwait(false);
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

        var path = ResolvePath();
        await _fileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureDirectoryExists(path);
            await using var stream = await OpenExclusiveAsync(path, cancellationToken).ConfigureAwait(false);
            var document = await ReadDocumentAsync(path, stream, cancellationToken).ConfigureAwait(false);
            document.Secrets[BuildTargetName(name)] = Encrypt(value);
            await WriteDocumentAsync(stream, document, cancellationToken).ConfigureAwait(false);
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

        var path = ResolvePath();
        await _fileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var stream = await OpenIfExistsExclusiveAsync(path, FileAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
            if (stream is null)
            {
                return;
            }

            var document = await ReadDocumentAsync(path, stream, cancellationToken).ConfigureAwait(false);
            if (document.Secrets.Remove(BuildTargetName(name)))
            {
                await WriteDocumentAsync(stream, document, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _fileGate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _fileGate.Dispose();

    private void EnsureDirectoryExists(string path)
    {
        var directory = _fileSystem.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            _fileSystem.Directory.CreateDirectory(directory);
        }
    }

    // Opens the store file exclusively across processes, creating it (owner-only on non-Windows) if it
    // does not yet exist. The exclusive hold on this one file is itself the cross-process lock, and the
    // write happens in place through the same handle - no second file is ever created.
    private async Task<Stream> OpenExclusiveAsync(string path, CancellationToken cancellationToken)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.OpenOrCreate,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None
        };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        while (true)
        {
            try
            {
                return _fileSystem.File.Open(path, options);
            }
            catch (IOException)
            {
                await Task.Delay(ExclusiveOpenRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    // Same exclusive hold as OpenExclusiveAsync, but never creates the file: reading (or removing from)
    // a store that was never written must not itself bring the file, or its directory, into existence.
    private async Task<Stream?> OpenIfExistsExclusiveAsync(string path, FileAccess access, CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                return _fileSystem.File.Open(path, FileMode.Open, access, FileShare.None);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
            catch (IOException)
            {
                await Task.Delay(ExclusiveOpenRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

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

    private async Task<SecretDocument> ReadDocumentAsync(string path, Stream stream, CancellationToken cancellationToken)
    {
        if (stream.Length == 0)
        {
            return new SecretDocument { Protector = Protector.Name };
        }

        stream.Position = 0;
        var document = await JsonSerializer.DeserializeAsync<SecretDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
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

    private async Task WriteDocumentAsync(Stream stream, SecretDocument document, CancellationToken cancellationToken)
    {
        document.Protector = Protector.Name;
        stream.Position = 0;
        await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken).ConfigureAwait(false);
        // The new content may be shorter than what was there before (e.g. a removed secret); writing in
        // place without truncating would leave trailing bytes from the old content after it.
        stream.SetLength(stream.Position);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private string ResolvePath()
    {
        if (!string.IsNullOrWhiteSpace(_fileOptions.Path))
        {
            return _fileOptions.Path;
        }

        var ns = string.IsNullOrEmpty(_options.Namespace) ? "saf" : _options.Namespace;

        // AppContext.BaseDirectory is the host application directory, which per docs/plugin-security.md
        // must be read-only to the runtime account - writing secrets there would need exactly the write
        // access that document forbids. Use the conventional per-machine data location instead; the
        // installer, not this provider, is responsible for creating it with the right permissions.
        var dataDirectory = OperatingSystem.IsWindows()
            ? _fileSystem.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), ns)
            : _fileSystem.Path.Combine("/var/lib", ns);

        return _fileSystem.Path.Combine(dataDirectory, "secrets.json");
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
