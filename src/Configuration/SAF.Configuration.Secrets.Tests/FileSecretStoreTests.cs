// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

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

    private FileSecretStore CreateStore(SecretStoreOptions? options = null, ISecretProtector? protector = null)
        => new(
            _fileSystem,
            Options.Create(options ?? new SecretStoreOptions()),
            Options.Create(new FileSecretStoreOptions { Path = StorePath }),
            NullLogger<FileSecretStore>.Instance,
            protector ?? new ReversingSecretProtector());

    private FileSecretStore CreateStoreWithoutProtector()
        => new(
            _fileSystem,
            Options.Create(new SecretStoreOptions()),
            Options.Create(new FileSecretStoreOptions { Path = StorePath }),
            NullLogger<FileSecretStore>.Instance,
            protector: null);
}
