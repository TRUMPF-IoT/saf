// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.FileStore;

using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// A cross-platform <see cref="ISecretStoreProvider"/> that persists secrets to a single JSON file.
/// Each value is encrypted at rest through an injected <see cref="ISecretProtector"/> (PKCS#7/CMS by
/// default); the logical names remain in clear, matching the security model that a secret reference is
/// not itself sensitive. File permissions are the installer's responsibility, not this provider's: a
/// first write creates the file owner-only (0600) on non-Windows, while on Windows it simply inherits
/// the ACL of its directory, which the installer is expected to create and restrict.
/// Every write happens in place through a single handle on the store file itself - no temporary or
/// sidecar file is ever created - so the provider also works on deployment targets that only permit
/// writing an already-existing file. The document is serialized to memory and written in one call, so a
/// serialization failure or a cancellation leaves the previous content intact; a process crash or a full
/// disk during that single write can still truncate the file, an accepted trade-off for this constraint.
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

    // Timestamp granularity of the coarsest filesystem this can be deployed on: 2 s on FAT/exFAT, 1 s on
    // ext3, ~16 ms on NTFS. Two writes within one bucket share a LastWriteTimeUtc, and an equal-length
    // rotation leaves Length unchanged too, so a stamp taken that soon after a write proves nothing.
    private static readonly TimeSpan WriteStampSettleWindow = TimeSpan.FromSeconds(2);

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

    // The parsed store file, kept between reads and revalidated against the file's write stamp. Only the
    // encrypted document is cached, never a decrypted value: resolving one configuration asks for every
    // reference in turn, and re-reading and re-parsing the whole file per lookup made that quadratic.
    // Guarded by _fileGate.
    private CachedDocument? _cachedDocument;

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
            var document = await ReadCachedDocumentAsync(path, cancellationToken).ConfigureAwait(false);
            return document is not null && document.Secrets.TryGetValue(name, out var encoded)
                ? Decrypt(path, name, encoded)
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
            document.Secrets[name] = Encrypt(value);
            await WriteDocumentAsync(stream, document, cancellationToken).ConfigureAwait(false);
            _cachedDocument = null;
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
            await using var stream = await OpenIfExistsExclusiveAsync(path, cancellationToken).ConfigureAwait(false);
            if (stream is null)
            {
                return;
            }

            var document = await ReadDocumentAsync(path, stream, cancellationToken).ConfigureAwait(false);
            if (document.Secrets.Remove(name))
            {
                await WriteDocumentAsync(stream, document, cancellationToken).ConfigureAwait(false);
                _cachedDocument = null;
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

        // Never null: nullIfMissing is false, so a missing file is created rather than reported.
        return (await OpenAsync(path, () => _fileSystem.File.Open(path, options), nullIfMissing: false, cancellationToken)
            .ConfigureAwait(false))!;
    }

    // Same exclusive hold as OpenExclusiveAsync, but never creates the file: removing from a store that
    // was never written must not itself bring the file, or its directory, into existence.
    private Task<Stream?> OpenIfExistsExclusiveAsync(string path, CancellationToken cancellationToken)
        => OpenAsync(path, () => _fileSystem.File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None), nullIfMissing: true, cancellationToken);

    // The read path shares with other readers: two hosts pointed at the same store file must be able to
    // resolve their configuration concurrently instead of contending at startup. A writer still holds
    // FileShare.None and so excludes readers for the duration of its in-place write.
    private Task<Stream?> OpenIfExistsForReadAsync(string path, CancellationToken cancellationToken)
        => OpenAsync(path, () => _fileSystem.File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read), nullIfMissing: true, cancellationToken);

    // Retries a sharing conflict only, and only until LockTimeout elapses. Retrying every IOException
    // forever turned a bad path or a foreign reader into an unkillable spin that also held _fileGate.
    private async Task<Stream?> OpenAsync(
        string path,
        Func<Stream> open,
        bool nullIfMissing,
        CancellationToken cancellationToken)
    {
        var timeout = _fileOptions.LockTimeout;
        var deadline = timeout == Timeout.InfiniteTimeSpan ? (long?)null : Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        var waiting = false;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return open();
            }
            catch (IOException e) when (nullIfMissing && IsMissingPathException(e))
            {
                return null;
            }
            catch (UnauthorizedAccessException e)
            {
                throw CreateAccessDeniedException(path, e);
            }
            catch (IOException e) when (IsSharingConflict(e))
            {
                waiting = await WaitForSharingConflictAsync(path, timeout, deadline, waiting, e, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static bool IsMissingPathException(IOException exception)
        => exception is FileNotFoundException or DirectoryNotFoundException;

    private static InvalidOperationException CreateAccessDeniedException(string path, UnauthorizedAccessException exception)
        => new(
            $"Access to the secret store file '{path}' was denied. The runtime account needs read " +
            "(and, for writes, write) access to it; granting that is the installer's responsibility.", exception);

    private async Task<bool> WaitForSharingConflictAsync(
        string path,
        TimeSpan timeout,
        long? deadline,
        bool waiting,
        IOException exception,
        CancellationToken cancellationToken)
    {
        ThrowIfLockTimeoutExceeded(path, timeout, deadline, exception);

        if (!waiting)
        {
            _logger.LogInformation(
                "Waiting for another process to release the secret store file {Path} (up to {Timeout}).",
                path,
                timeout);
            waiting = true;
        }

        await Task.Delay(ExclusiveOpenRetryDelay, cancellationToken).ConfigureAwait(false);
        return waiting;
    }

    private static void ThrowIfLockTimeoutExceeded(string path, TimeSpan timeout, long? deadline, IOException exception)
    {
        if (deadline is not null && Environment.TickCount64 >= deadline)
        {
            throw new TimeoutException(
                $"The secret store file '{path}' stayed locked by another process for longer than " +
                $"{timeout} ({nameof(FileSecretStoreOptions)}.{nameof(FileSecretStoreOptions.LockTimeout)}).",
                exception);
        }
    }

    // A missing path, an unreachable drive or an over-long path all surface as IOException but will never
    // clear by waiting, so they must escape the retry loop rather than be polled.
    private static bool IsSharingConflict(IOException exception)
        => exception is not (FileNotFoundException or DirectoryNotFoundException or PathTooLongException or DriveNotFoundException);

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

    private string Decrypt(string path, string targetName, string encoded)
    {
        byte[] enveloped;
        try
        {
            enveloped = Convert.FromBase64String(encoded);
        }
        catch (FormatException e)
        {
            throw new InvalidOperationException(
                $"The stored value of secret '{targetName}' in '{path}' is not valid Base64 and cannot be " +
                "decrypted. An interrupted write can corrupt it; re-provision that secret.", e);
        }

        var plaintext = Protector.Unprotect(enveloped);
        try
        {
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            Array.Clear(plaintext);
        }
    }

    // Returns the parsed store file, re-reading it only when its write stamp no longer matches the cached
    // one. A stamp mismatch is the only way another process's write is noticed; this process invalidates
    // the cache itself whenever it writes.
    private async Task<SecretDocument?> ReadCachedDocumentAsync(string path, CancellationToken cancellationToken)
    {
        var stamp = GetWriteStamp(path);
        if (stamp is null)
        {
            _cachedDocument = null;
            return null;
        }

        if (_cachedDocument is { } cached && cached.Stamp == stamp.Value)
        {
            return cached.Document;
        }

        await using var stream = await OpenIfExistsForReadAsync(path, cancellationToken).ConfigureAwait(false);
        if (stream is null)
        {
            _cachedDocument = null;
            return null;
        }

        var document = await ReadDocumentAsync(path, stream, cancellationToken).ConfigureAwait(false);

        // Stamped from after the read, not from before it: a write that landed in between must invalidate
        // this entry rather than be masked by it. An unsettled stamp is not cached at all - the next
        // lookup re-reads, which is the only way to notice a second write in the same bucket.
        var stampAfterRead = GetWriteStamp(path);
        _cachedDocument = stampAfterRead is not null && HasSettled(stampAfterRead.Value)
            ? new CachedDocument(document, stampAfterRead.Value)
            : null;
        return document;
    }

    private (DateTime LastWriteTimeUtc, long Length)? GetWriteStamp(string path)
    {
        var info = _fileSystem.FileInfo.New(path);
        return info.Exists ? (info.LastWriteTimeUtc, info.Length) : null;
    }

    // A stamp identifies the content only once no further write can share it, which is the case as soon
    // as its write time lies further in the past than the filesystem's timestamp granularity. Until then
    // an equal stamp is not evidence of equal content: a rotation to a same-length value would be invisible.
    private static bool HasSettled((DateTime LastWriteTimeUtc, long Length) stamp)
        => DateTime.UtcNow - stamp.LastWriteTimeUtc > WriteStampSettleWindow;

    private sealed record CachedDocument(SecretDocument Document, (DateTime LastWriteTimeUtc, long Length) Stamp);

    private async Task<SecretDocument> ReadDocumentAsync(string path, Stream stream, CancellationToken cancellationToken)
    {
        if (stream.Length == 0)
        {
            return new SecretDocument { Protector = Protector.Name };
        }

        stream.Position = 0;
        SecretDocument? parsed;
        try
        {
            parsed = await JsonSerializer.DeserializeAsync<SecretDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        // InvalidOperationException as well as JsonException: a "secrets": null member is rejected by the
        // serializer, and used to surface as a bare NullReferenceException naming nothing at all.
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"The secret store file '{path}' could not be parsed. An interrupted write can leave it " +
                "truncated or corrupt; restore it from a backup or re-provision its secrets.", e);
        }

        var document = parsed ?? new SecretDocument { Protector = Protector.Name };

        if (!string.IsNullOrEmpty(document.Protector)
            && !string.Equals(document.Protector, Protector.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The secret store file '{path}' was written by protector '{document.Protector}', but the " +
                $"configured protector is '{Protector.Name}'. Its secrets cannot be decrypted with the current configuration.");
        }

        return document;
    }

    // Serialized to memory first because the live file is the only copy: a serialization failure or a
    // cancellation must not be able to leave half a document in it. The token guards the buffering, not
    // the write - once the first byte is out, honouring it would corrupt the store.
    private async Task WriteDocumentAsync(Stream stream, SecretDocument document, CancellationToken cancellationToken)
    {
        document.Protector = Protector.Name;

        using var buffer = new MemoryStream();
        await JsonSerializer.SerializeAsync(buffer, document, JsonOptions, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        stream.Position = 0;
        await stream.WriteAsync(buffer.GetBuffer().AsMemory(0, (int)buffer.Length), CancellationToken.None).ConfigureAwait(false);
        // The new content may be shorter than what was there before (e.g. a removed secret); writing in
        // place without truncating would leave trailing bytes from the old content after it.
        stream.SetLength(stream.Position);
        await stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
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

    /// <summary>The on-disk shape of the secret store file: encrypted values keyed by target name.</summary>
    private sealed class SecretDocument
    {
        /// <summary>The name of the protector that enveloped the values, stamped for read-time validation.</summary>
        public string? Protector { get; set; }

        /// <summary>Base64-encoded protected payloads keyed by the namespaced target name.</summary>
        // Populated, not replaced: by default System.Text.Json assigns a fresh, case-sensitive dictionary
        // over the initializer, which silently dropped the comparer for every file it read. Get-only so
        // that a null in the JSON cannot null the property either.
        [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
        public Dictionary<string, string> Secrets { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
