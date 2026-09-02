// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SAF.Configuration.Secrets.Contracts;
using SAF.Configuration.Secrets.FileStore;
using Testably.Abstractions.Testing;
using Xunit;

public class FileSecretStoreTests
{
    private const string StorePath = "/data/secrets/saf.secrets.json";

    private readonly MockFileSystem _fileSystem = new();

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetSecretAsync_ReturnsNull_WhenFileDoesNotExist()
    {
        var store = CreateStore();

        var result = await store.GetSecretAsync("conn/pw", TestToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSecretAsync_ReturnsNull_WhenSecretMissing()
    {
        var store = CreateStore();
        await store.SetSecretAsync("conn/pw", "value", TestToken);

        var result = await store.GetSecretAsync("other/pw", TestToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task SetThenGet_RoundTripsSecret()
    {
        var store = CreateStore();

        await store.SetSecretAsync("conn/pw", "s3cr3t-äöü", TestToken);
        var result = await store.GetSecretAsync("conn/pw", TestToken);

        Assert.Equal("s3cr3t-äöü", result);
    }

    [Fact]
    public async Task SetSecretAsync_CreatesStoreDirectory()
    {
        var store = CreateStore();

        await store.SetSecretAsync("conn/pw", "value", TestToken);

        Assert.True(_fileSystem.File.Exists(StorePath));
    }

    [Fact]
    public async Task ResolvePath_DefaultsToPerMachineDataLocation_WhenPathNotConfigured()
    {
        var store = CreateStoreWithDefaultPath(new SecretStoreOptions { Namespace = "myapp" });

        await store.SetSecretAsync("conn/pw", "value", TestToken);

        var dataDirectory = OperatingSystem.IsWindows()
            ? _fileSystem.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "myapp")
            : _fileSystem.Path.Combine("/var/lib", "myapp");
        var expectedPath = _fileSystem.Path.Combine(dataDirectory, "secrets.json");

        Assert.True(_fileSystem.File.Exists(expectedPath));
        Assert.False(expectedPath.StartsWith(AppContext.BaseDirectory, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SetSecretAsync_OverwritesExistingSecret()
    {
        var store = CreateStore();

        await store.SetSecretAsync("conn/pw", "first", TestToken);
        await store.SetSecretAsync("conn/pw", "second", TestToken);

        Assert.Equal("second", await store.GetSecretAsync("conn/pw", TestToken));
    }

    [Fact]
    public async Task SetSecretAsync_KeepsOtherSecrets()
    {
        var store = CreateStore();

        await store.SetSecretAsync("conn/pw", "one", TestToken);
        await store.SetSecretAsync("conn/user", "two", TestToken);

        Assert.Equal("one", await store.GetSecretAsync("conn/pw", TestToken));
        Assert.Equal("two", await store.GetSecretAsync("conn/user", TestToken));
    }

    [Fact]
    public async Task StoredFile_DoesNotContainPlaintext_AndKeysByTheTargetNameItWasGiven()
    {
        var store = CreateStore();

        await store.SetSecretAsync("saf/conn/pw", "top-secret-value", TestToken);

        var content = await _fileSystem.File.ReadAllTextAsync(StorePath, TestToken);
        Assert.DoesNotContain("top-secret-value", content);
        Assert.Contains("saf/conn/pw", content);
        // The composite store namespaced the name already; applying it again would key "saf/saf/conn/pw".
        Assert.DoesNotContain("saf/saf/conn/pw", content);
        Assert.Contains("\"fake\"", content); // stamped protector name
    }

    [Fact]
    public async Task RemoveSecretAsync_DeletesSecret()
    {
        var store = CreateStore();
        await store.SetSecretAsync("conn/pw", "value", TestToken);

        await store.RemoveSecretAsync("conn/pw", TestToken);

        Assert.Null(await store.GetSecretAsync("conn/pw", TestToken));
    }

    [Fact]
    public async Task RemoveSecretAsync_DoesNotThrow_WhenFileMissing()
    {
        var store = CreateStore();

        await store.RemoveSecretAsync("conn/pw", TestToken);

        Assert.False(_fileSystem.File.Exists(StorePath));
    }

    [Fact]
    public async Task GetSecretAsync_IsCaseInsensitive_ForTheTargetName()
    {
        var store = CreateStore();
        await store.SetSecretAsync("MyApp/Conn/PW", "value", TestToken);

        // A second instance has to re-read the file, so this also pins the comparer surviving the parse.
        Assert.Equal("value", await CreateStore().GetSecretAsync("myapp/conn/pw", TestToken));
    }

    [Fact]
    public async Task ReadDocument_Throws_OnProtectorMismatch()
    {
        var store = CreateStore();
        await store.SetSecretAsync("conn/pw", "value", TestToken);

        // A store configured with a different protector must refuse the file rather than fail obscurely.
        var otherStore = CreateStore(protector: new ReversingSecretProtector("dpapi"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await otherStore.GetSecretAsync("conn/pw", TestToken));
        Assert.Contains("protector", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RoundTrips_WithRealPkcsProtector()
    {
        var certificate = TestCertificates.CreateRsaCertificate();
        try
        {
            var store = CreateStore(protector: new Protection.PkcsSecretProtector(certificate));

            await store.SetSecretAsync("conn/pw", "real-cms-value", TestToken);
            var result = await store.GetSecretAsync("conn/pw", TestToken);

            Assert.Equal("real-cms-value", result);
            Assert.DoesNotContain("real-cms-value", await _fileSystem.File.ReadAllTextAsync(StorePath, TestToken));
        }
        finally
        {
            TestCertificates.DisposeAndDeleteKey(certificate);
        }
    }

    [Fact]
    public void Name_IsStableProviderIdentifier()
    {
        Assert.Equal("file", CreateStore().Name);
        Assert.Equal(FileSecretStore.ProviderName, CreateStore().Name);
    }

    [Fact]
    public void IsAvailable_True_WhenProtectorConfigured()
        => Assert.True(CreateStore().IsAvailable);

    [Fact]
    public void IsAvailable_False_WhenProtectorMissing()
        => Assert.False(CreateStoreWithoutProtector().IsAvailable);

    [Fact]
    public async Task Operations_Throw_WhenProtectorMissing()
    {
        var store = CreateStoreWithoutProtector();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await store.SetSecretAsync("conn/pw", "value", TestToken));
        Assert.Contains(nameof(ISecretProtector), ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetSecretAsync_Throws_OnInvalidName(string? name)
    {
        var store = CreateStore();

        await Assert.ThrowsAnyAsync<ArgumentException>(async () => await store.GetSecretAsync(name!, TestToken));
    }

    [Fact]
    public async Task SetSecretAsync_Throws_OnNullValue()
    {
        var store = CreateStore();

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await store.SetSecretAsync("conn/pw", null!, TestToken));
    }

    [Fact]
    public async Task GetSecretAsync_Throws_WhenCancelled()
    {
        var store = CreateStore();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.GetSecretAsync("conn/pw", cts.Token));
    }

    [Fact]
    public void Constructor_Throws_OnNullDependencies()
    {
        var options = Options.Create(new SecretStoreOptions());
        var fileOptions = Options.Create(new FileSecretStoreOptions { Path = StorePath });
        var logger = NullLogger<FileSecretStore>.Instance;
        var protector = new ReversingSecretProtector();

        Assert.Throws<ArgumentNullException>(() => new FileSecretStore(null!, options, fileOptions, logger, protector));
        Assert.Throws<ArgumentNullException>(() => new FileSecretStore(_fileSystem, null!, fileOptions, logger, protector));
        Assert.Throws<ArgumentNullException>(() => new FileSecretStore(_fileSystem, options, null!, logger, protector));
        Assert.Throws<ArgumentNullException>(() => new FileSecretStore(_fileSystem, options, fileOptions, null!, protector));
    }

    [Fact]
    public void Constructor_AllowsNullProtector()
        => Assert.NotNull(CreateStoreWithoutProtector());

    [Fact]
    public async Task SetSecretAsync_LeavesNoTempFiles_AfterSeveralWrites()
    {
        var store = CreateStore();

        await store.SetSecretAsync("conn/pw", "one", TestToken);
        await store.SetSecretAsync("conn/pw", "two", TestToken);
        await store.SetSecretAsync("conn/user", "three", TestToken);

        var directory = _fileSystem.Path.GetDirectoryName(StorePath)!;
        Assert.DoesNotContain(
            _fileSystem.Directory.GetFiles(directory),
            f => f.EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SetSecretAsync_PreservesExistingUnixFileMode_OnOverwrite()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes only apply on non-Windows platforms.");
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var store = CreateStore();
        await store.SetSecretAsync("conn/pw", "first", TestToken);
        var restrictedMode = UnixFileMode.UserRead;
        _fileSystem.File.SetUnixFileMode(StorePath, restrictedMode);

        await store.SetSecretAsync("conn/pw", "second", TestToken);

        Assert.Equal(restrictedMode, _fileSystem.File.GetUnixFileMode(StorePath));
    }

    // The "restrictive mode for a brand-new file" behavior (FileStreamOptions.UnixCreateMode) is
    // covered by FileSecretStoreIntegrationTests instead: MockFileSystem does not apply UnixCreateMode
    // when simulating Linux, which would make this assertion fail here for a reason unrelated to the
    // production code (confirmed empirically against a real Linux file system).

    [Fact]
    public async Task SetSecretAsync_WaitsForCrossProcessLock_ThenSucceeds()
    {
        var store = CreateStore();
        await store.SetSecretAsync("conn/pw", "first", TestToken);
        // No sidecar lock file exists: the store file itself, opened exclusively, is the lock. This
        // stands in for a second process (e.g. an installer) holding it open concurrently.
        var externalLock = _fileSystem.File.Open(
            StorePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var setTask = store.SetSecretAsync("conn/pw", "second", TestToken);
        await Task.Delay(100, TestToken);
        Assert.False(setTask.IsCompleted, "the write must block while another process holds the lock");

        externalLock.Dispose();
        await setTask;

        Assert.Equal("second", await store.GetSecretAsync("conn/pw", TestToken));
    }

    [Fact]
    public async Task SetSecretAsync_Throws_WhenCancelledWhileWaitingForCrossProcessLock()
    {
        var store = CreateStore();
        await store.SetSecretAsync("conn/pw", "first", TestToken);
        using var externalLock = _fileSystem.File.Open(
            StorePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.SetSecretAsync("conn/pw", "second", cts.Token));
    }

    [Fact]
    public async Task GetSecretAsync_ServesTheParsedDocument_WhileTheStoreFileIsUnchanged()
    {
        // Resolving one configuration asks for every reference in turn; re-reading and re-parsing the whole
        // file per lookup made that quadratic in the number of references.
        var store = CreateStore();
        await store.SetSecretAsync("conn/a", "aaaa", TestToken);
        await store.SetSecretAsync("conn/b", "bbbb", TestToken);
        var stamp = SettleWriteStamp();
        Assert.Equal("aaaa", await store.GetSecretAsync("conn/a", TestToken));

        // Swap the two ciphertexts behind the store's back, then put the settled stamp back: same length,
        // same timestamp, so nothing signals a change and the parsed document must still be used.
        var text = await _fileSystem.File.ReadAllTextAsync(StorePath, TestToken);
        await _fileSystem.File.WriteAllTextAsync(
            StorePath,
            text.Replace(Encode("aaaa"), "@@").Replace(Encode("bbbb"), Encode("aaaa")).Replace("@@", Encode("bbbb")),
            TestToken);
        _fileSystem.File.SetLastWriteTimeUtc(StorePath, stamp);

        Assert.Equal("aaaa", await store.GetSecretAsync("conn/a", TestToken));
    }

    [Fact]
    public async Task GetSecretAsync_SeesARotation_ThatLeftTheWriteStampUnchanged()
    {
        // A fixed-size key wrap around AES-CBC envelopes an equal-length value to an equal-length
        // ciphertext, so rotating to a same-length password changes Length not at all and - within one
        // timestamp bucket, 2 s on FAT/exFAT and ~16 ms on NTFS - LastWriteTimeUtc neither. A stamp taken
        // that soon after a write therefore cannot be used to prove the cached document is still current.
        var reader = CreateStore();
        var writer = CreateStore();
        await writer.SetSecretAsync("conn/pw", "first-pw", TestToken);
        var stamp = _fileSystem.FileInfo.New(StorePath).LastWriteTimeUtc;
        var length = _fileSystem.FileInfo.New(StorePath).Length;
        Assert.Equal("first-pw", await reader.GetSecretAsync("conn/pw", TestToken));

        await writer.SetSecretAsync("conn/pw", "later-pw", TestToken);
        _fileSystem.File.SetLastWriteTimeUtc(StorePath, stamp);
        // The premise: neither half of the stamp can distinguish the two documents.
        Assert.Equal(length, _fileSystem.FileInfo.New(StorePath).Length);

        Assert.Equal("later-pw", await reader.GetSecretAsync("conn/pw", TestToken));
    }

    [Fact]
    public async Task GetSecretAsync_ReReadsTheStoreFile_AfterAnotherInstanceWroteIt()
    {
        var reader = CreateStore();
        var writer = CreateStore();
        await writer.SetSecretAsync("conn/pw", "first", TestToken);
        Assert.Equal("first", await reader.GetSecretAsync("conn/pw", TestToken));

        await writer.SetSecretAsync("conn/pw", "a-much-longer-second-value", TestToken);

        Assert.Equal("a-much-longer-second-value", await reader.GetSecretAsync("conn/pw", TestToken));
    }

    [Fact]
    public async Task SetSecretAsync_InvalidatesTheParsedDocument()
    {
        var store = CreateStore();
        await store.SetSecretAsync("conn/pw", "first", TestToken);
        Assert.Equal("first", await store.GetSecretAsync("conn/pw", TestToken));

        await store.SetSecretAsync("conn/pw", "second", TestToken);

        Assert.Equal("second", await store.GetSecretAsync("conn/pw", TestToken));
    }

    [Fact]
    public async Task RemoveSecretAsync_InvalidatesTheParsedDocument()
    {
        var store = CreateStore();
        await store.SetSecretAsync("conn/pw", "value", TestToken);
        Assert.Equal("value", await store.GetSecretAsync("conn/pw", TestToken));

        await store.RemoveSecretAsync("conn/pw", TestToken);

        Assert.Null(await store.GetSecretAsync("conn/pw", TestToken));
    }

    [Fact]
    public async Task GetSecretAsync_ReturnsNull_AfterTheStoreFileIsDeleted()
    {
        var store = CreateStore();
        await store.SetSecretAsync("conn/pw", "value", TestToken);
        Assert.Equal("value", await store.GetSecretAsync("conn/pw", TestToken));

        _fileSystem.File.Delete(StorePath);

        Assert.Null(await store.GetSecretAsync("conn/pw", TestToken));
    }

    [Fact]
    public async Task GetSecretAsync_FindsSecretWrittenInDifferentCase_InAHandProvisionedFile()
    {
        // The documented promise: "The physical store key is case-insensitive". Only a hand-edited or
        // externally provisioned file exercises it - the store itself always writes lower-invariant keys.
        await WriteStoreFileAsync(Provisioned("MyApp/Db/Password", "s3cret"));
        var store = CreateStore();

        Assert.Equal("s3cret", await store.GetSecretAsync("myapp/db/password", TestToken));
    }

    [Fact]
    public async Task SetSecretAsync_OverwritesAHandProvisionedSecret_RatherThanDuplicatingIt()
    {
        await WriteStoreFileAsync(Provisioned("MyApp/Db/Password", "s3cret"));
        var store = CreateStore();

        await store.SetSecretAsync("myapp/db/password", "rotated", TestToken);

        Assert.Equal("rotated", await store.GetSecretAsync("myapp/db/password", TestToken));
        Assert.DoesNotContain(Encode("s3cret"), await _fileSystem.File.ReadAllTextAsync(StorePath, TestToken));
    }

    [Fact]
    public async Task GetSecretAsync_ThrowsNamingTheFile_WhenTheDocumentIsTruncated()
    {
        await WriteStoreFileAsync(Provisioned("saf/conn/pw", "value")[..20]);
        var store = CreateStore();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await store.GetSecretAsync("conn/pw", TestToken));

        Assert.Contains(StorePath, ex.Message);
        Assert.IsType<JsonException>(ex.InnerException);
    }

    [Fact]
    public async Task GetSecretAsync_ThrowsNamingTheFile_WhenTheDocumentHasANullSecretsMember()
    {
        // Used to dereference the replaced-with-null dictionary and take down host startup with a bare
        // NullReferenceException naming neither the store, the file nor the secret.
        await WriteStoreFileAsync("{\"Protector\":\"fake\",\"Secrets\":null}");
        var store = CreateStore();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await store.GetSecretAsync("conn/pw", TestToken));

        Assert.Contains(StorePath, ex.Message);
    }

    [Fact]
    public async Task GetSecretAsync_ThrowsNamingTheSecret_WhenItsPayloadIsNotBase64()
    {
        await WriteStoreFileAsync("{\"Protector\":\"fake\",\"Secrets\":{\"saf/conn/pw\":\"not base64!\"}}");
        var store = CreateStore();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await store.GetSecretAsync("saf/conn/pw", TestToken));

        Assert.Contains("saf/conn/pw", ex.Message);
        Assert.Contains(StorePath, ex.Message);
    }

    [Fact]
    public async Task SetSecretAsync_LeavesThePreviousContentIntact_WhenCancelledBeforeWriting()
    {
        var store = CreateStore();
        await store.SetSecretAsync("conn/pw", "first", TestToken);
        var before = await _fileSystem.File.ReadAllTextAsync(StorePath, TestToken);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.SetSecretAsync("conn/pw", "second", cts.Token));

        Assert.Equal(before, await _fileSystem.File.ReadAllTextAsync(StorePath, TestToken));
        Assert.Equal("first", await store.GetSecretAsync("conn/pw", TestToken));
    }

    [Fact]
    public async Task GetSecretAsync_SharesTheStoreFileWithOtherReaders()
    {
        // Two hosts resolving their configuration from one store file must not contend at startup.
        var store = CreateStore();
        await store.SetSecretAsync("conn/pw", "value", TestToken);
        using var concurrentReader = _fileSystem.File.Open(
            StorePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        Assert.Equal("value", await store.GetSecretAsync("conn/pw", TestToken));
    }

    [Fact]
    public async Task GetSecretAsync_TimesOut_WhenAnotherProcessKeepsTheStoreFileLocked()
    {
        var store = CreateStore(lockTimeout: TimeSpan.FromMilliseconds(150));
        await store.SetSecretAsync("conn/pw", "value", TestToken);
        using var externalLock = _fileSystem.File.Open(
            StorePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var ex = await Assert.ThrowsAsync<TimeoutException>(
            async () => await store.GetSecretAsync("conn/pw", TestToken));

        Assert.Contains(StorePath, ex.Message);
        Assert.Contains(nameof(FileSecretStoreOptions.LockTimeout), ex.Message);
    }

    [Fact]
    public async Task SetSecretAsync_TimesOut_WhenAnotherProcessKeepsTheStoreFileLocked()
    {
        var store = CreateStore(lockTimeout: TimeSpan.FromMilliseconds(150));
        await store.SetSecretAsync("conn/pw", "first", TestToken);
        using var externalLock = _fileSystem.File.Open(
            StorePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        await Assert.ThrowsAsync<TimeoutException>(
            async () => await store.SetSecretAsync("conn/pw", "second", TestToken));
    }

    [Fact]
    public async Task SecretOperations_RecoverAfterATimeout_RatherThanStayingBlocked()
    {
        // The retry loop runs while _fileGate is held, so an unbounded wait stalled every other secret
        // operation in the process, not just the contended one.
        var store = CreateStore(lockTimeout: TimeSpan.FromMilliseconds(150));
        await store.SetSecretAsync("conn/pw", "first", TestToken);
        var externalLock = _fileSystem.File.Open(
            StorePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        await Assert.ThrowsAsync<TimeoutException>(
            async () => await store.SetSecretAsync("conn/pw", "second", TestToken));
        externalLock.Dispose();

        Assert.Equal("first", await store.GetSecretAsync("conn/pw", TestToken));
    }

    [Fact]
    public async Task SetSecretAsync_ReportsAnUnopenablePath_InsteadOfRetryingItForever()
    {
        // An access failure will never clear by waiting, so it must escape the retry loop even when the
        // caller asked to wait forever - and be reported by the store rather than escape it raw.
        var store = CreateStore(path: "/data/secrets/dir.json", lockTimeout: Timeout.InfiniteTimeSpan);
        _fileSystem.Directory.CreateDirectory("/data/secrets/dir.json");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await store.SetSecretAsync("conn/pw", "value", TestToken));

        Assert.Contains("/data/secrets/dir.json", ex.Message);
        Assert.IsType<UnauthorizedAccessException>(ex.InnerException);
    }

    [Fact]
    public async Task GetSecretAsync_ReturnsNull_WhenTheStoreDirectoryDoesNotExist()
    {
        var store = CreateStore(path: "/nowhere/secrets.json", lockTimeout: Timeout.InfiniteTimeSpan);

        Assert.Null(await store.GetSecretAsync("conn/pw", TestToken));
    }

    [Theory]
    [MemberData(nameof(NonTransientOpenFailures))]
    public async Task SetSecretAsync_DoesNotRetry_AnIOExceptionThatCannotClear(Exception failure)
    {
        // PathTooLongException, DriveNotFoundException and DirectoryNotFoundException are all IOException,
        // so retrying every IOException polled them forever. LockTimeout is infinite here on purpose: only
        // classifying the exception, not the deadline, can end this wait.
        var fileSystem = Substitute.For<IFileSystem>();
        fileSystem.File.Open(Arg.Any<string>(), Arg.Any<FileStreamOptions>()).Throws(failure);
        var store = new FileSecretStore(
            fileSystem,
            Options.Create(new SecretStoreOptions()),
            Options.Create(new FileSecretStoreOptions { Path = StorePath, LockTimeout = Timeout.InfiniteTimeSpan }),
            NullLogger<FileSecretStore>.Instance,
            new ReversingSecretProtector());

        var thrown = await Assert.ThrowsAnyAsync<IOException>(
            async () => await store.SetSecretAsync("conn/pw", "value", TestToken));

        Assert.Same(failure, thrown);
    }

    public static TheoryData<Exception> NonTransientOpenFailures =>
    [
        new PathTooLongException("the path is too long"),
        new DriveNotFoundException("the drive does not exist"),
        new DirectoryNotFoundException("the directory does not exist")
    ];

    // The shape FileSecretStore expects on disk, as an installer or an admin would hand-write it.
    private static string Provisioned(string key, string value)
        => $"{{\"Protector\":\"fake\",\"Secrets\":{{\"{key}\":\"{Encode(value)}\"}}}}";

    // Backdates the store file past the settle window, so its write stamp identifies the content and may
    // be cached against. Without this, every read re-reads - which is what makes a same-bucket write safe.
    private DateTime SettleWriteStamp()
    {
        var settled = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        _fileSystem.File.SetLastWriteTimeUtc(StorePath, settled);
        return settled;
    }

    private Task WriteStoreFileAsync(string json)
    {
        _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(StorePath)!);
        return _fileSystem.File.WriteAllTextAsync(StorePath, json, TestToken);
    }

    // Mirrors FileSecretStore's on-disk encoding for the ReversingSecretProtector used in these tests.
    private static string Encode(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Array.Reverse(bytes);
        return Convert.ToBase64String(bytes);
    }

    private FileSecretStore CreateStore(
        SecretStoreOptions? options = null,
        ISecretProtector? protector = null,
        string? path = null,
        TimeSpan? lockTimeout = null)
        => new(
            _fileSystem,
            Options.Create(options ?? new SecretStoreOptions()),
            Options.Create(new FileSecretStoreOptions
            {
                Path = path ?? StorePath,
                LockTimeout = lockTimeout ?? new FileSecretStoreOptions().LockTimeout
            }),
            NullLogger<FileSecretStore>.Instance,
            protector ?? new ReversingSecretProtector());

    private FileSecretStore CreateStoreWithDefaultPath(SecretStoreOptions options)
        => new(
            _fileSystem,
            Options.Create(options),
            Options.Create(new FileSecretStoreOptions()),
            NullLogger<FileSecretStore>.Instance,
            new ReversingSecretProtector());

    private FileSecretStore CreateStoreWithoutProtector()
        => new(
            _fileSystem,
            Options.Create(new SecretStoreOptions()),
            Options.Create(new FileSecretStoreOptions { Path = StorePath }),
            NullLogger<FileSecretStore>.Instance,
            protector: null);
}
