// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using System.IO;
using System.IO.Abstractions;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
    public async Task StoredFile_DoesNotContainPlaintext_AndUsesNamespacedKey()
    {
        var store = CreateStore();

        await store.SetSecretAsync("conn/pw", "top-secret-value", TestToken);

        var content = await _fileSystem.File.ReadAllTextAsync(StorePath, TestToken);
        Assert.DoesNotContain("top-secret-value", content);
        Assert.Contains("saf/conn/pw", content);
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
    public async Task Operations_UseRawName_WhenNamespaceIsEmpty()
    {
        var store = CreateStore(new SecretStoreOptions { Namespace = string.Empty });

        await store.SetSecretAsync("conn/pw", "value", TestToken);

        var content = await _fileSystem.File.ReadAllTextAsync(StorePath, TestToken);
        Assert.Contains("\"conn/pw\"", content);
        Assert.DoesNotContain("saf/conn/pw", content);
    }

    [Fact]
    public async Task GetSecretAsync_IsCaseInsensitive_ForNamespaceAndName()
    {
        var store = CreateStore(new SecretStoreOptions { Namespace = "MyApp" });
        await store.SetSecretAsync("Conn/PW", "value", TestToken);

        var otherCasing = CreateStore(new SecretStoreOptions { Namespace = "myapp" });

        Assert.Equal("value", await otherCasing.GetSecretAsync("conn/pw", TestToken));
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
        using var certificate = TestCertificates.CreateRsaCertificate();
        var store = CreateStore(protector: new Protection.PkcsSecretProtector(certificate));

        await store.SetSecretAsync("conn/pw", "real-cms-value", TestToken);
        var result = await store.GetSecretAsync("conn/pw", TestToken);

        Assert.Equal("real-cms-value", result);
        Assert.DoesNotContain("real-cms-value", await _fileSystem.File.ReadAllTextAsync(StorePath, TestToken));
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

    private FileSecretStore CreateStore(SecretStoreOptions? options = null, ISecretProtector? protector = null)
        => new(
            _fileSystem,
            Options.Create(options ?? new SecretStoreOptions()),
            Options.Create(new FileSecretStoreOptions { Path = StorePath }),
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
