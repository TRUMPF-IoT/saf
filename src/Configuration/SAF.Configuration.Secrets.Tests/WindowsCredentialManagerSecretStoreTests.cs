// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SAF.Configuration.Secrets.Contracts;
using SAF.Configuration.Secrets.WindowsCredentialManager;
using Xunit;

public class WindowsCredentialManagerSecretStoreTests
{
    private readonly INativeCredentialApi _nativeApi = Substitute.For<INativeCredentialApi>();

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetSecretAsync_ReturnsValue_WhenCredentialExists()
    {
        _nativeApi
            .TryReadGenericCredential("saf/conn/pw", out Arg.Any<string?>())
            .Returns(call => { call[1] = "s3cr3t"; return true; });
        var store = CreateStore();

        var result = await store.GetSecretAsync("conn/pw", TestToken);

        Assert.Equal("s3cr3t", result);
    }

    [Fact]
    public async Task GetSecretAsync_ReturnsNull_WhenCredentialMissing()
    {
        _nativeApi
            .TryReadGenericCredential(Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(false);
        var store = CreateStore();

        var result = await store.GetSecretAsync("conn/pw", TestToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task SetSecretAsync_WritesNamespacedCredential()
    {
        var store = CreateStore();

        await store.SetSecretAsync("conn/pw", "value", TestToken);

        _nativeApi.Received(1).WriteGenericCredential("saf/conn/pw", "value");
    }

    [Fact]
    public async Task RemoveSecretAsync_DeletesNamespacedCredential()
    {
        var store = CreateStore();

        await store.RemoveSecretAsync("conn/pw", TestToken);

        _nativeApi.Received(1).DeleteGenericCredential("saf/conn/pw");
    }

    [Fact]
    public async Task RemoveSecretAsync_DoesNotThrow_WhenCredentialMissing()
    {
        _nativeApi.DeleteGenericCredential(Arg.Any<string>()).Returns(false);
        var store = CreateStore();

        await store.RemoveSecretAsync("conn/pw", TestToken);

        _nativeApi.Received(1).DeleteGenericCredential("saf/conn/pw");
    }

    [Fact]
    public async Task Operations_UseRawName_WhenNamespaceIsEmpty()
    {
        var store = CreateStore(new SecretStoreOptions { Namespace = string.Empty });

        await store.SetSecretAsync("conn/pw", "value", TestToken);

        _nativeApi.Received(1).WriteGenericCredential("conn/pw", "value");
    }

    [Fact]
    public async Task Operations_UseCustomNamespace()
    {
        var store = CreateStore(new SecretStoreOptions { Namespace = "myproduct" });

        await store.SetSecretAsync("conn/pw", "value", TestToken);

        _nativeApi.Received(1).WriteGenericCredential("myproduct/conn/pw", "value");
    }

    [Fact]
    public async Task SetSecretAsync_StillWrites_WhenScopeIsMachine()
    {
        // Machine scope is not achievable via Credential Manager; the store logs a warning but still
        // persists the secret into the running identity's vault.
        var store = CreateStore(new SecretStoreOptions { Scope = SecretScope.Machine });

        await store.SetSecretAsync("conn/pw", "value", TestToken);

        _nativeApi.Received(1).WriteGenericCredential("saf/conn/pw", "value");
    }

    [Fact]
    public void Name_IsStableProviderIdentifier()
    {
        Assert.Equal("windows-credential-manager", CreateStore().Name);
        Assert.Equal(WindowsCredentialManagerSecretStore.ProviderName, CreateStore().Name);
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
    public async Task SetSecretAsync_Throws_WhenValueExceedsBlobSizeLimit()
    {
        var store = CreateStore();
        var tooLong = new string('a', 1281); // 2562 bytes UTF-16, over the 2560-byte CRED_MAX_CREDENTIAL_BLOB_SIZE

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await store.SetSecretAsync("conn/pw", tooLong, TestToken));
        _nativeApi.DidNotReceive().WriteGenericCredential(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SetSecretAsync_Writes_WhenValueIsAtBlobSizeLimit()
    {
        var store = CreateStore();
        var atLimit = new string('a', 1280); // exactly 2560 bytes UTF-16

        await store.SetSecretAsync("conn/pw", atLimit, TestToken);

        _nativeApi.Received(1).WriteGenericCredential("saf/conn/pw", atLimit);
    }

    [Fact]
    public async Task SetSecretAsync_Throws_WhenTargetNameExceedsUsernameLimit()
    {
        var store = CreateStore(new SecretStoreOptions { Namespace = string.Empty });
        var tooLong = new string('a', 514); // over the 513-char CRED_MAX_USERNAME_LENGTH

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await store.SetSecretAsync(tooLong, "value", TestToken));
        _nativeApi.DidNotReceive().WriteGenericCredential(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SetSecretAsync_Writes_WhenTargetNameIsAtUsernameLimit()
    {
        var store = CreateStore(new SecretStoreOptions { Namespace = string.Empty });
        var atLimit = new string('a', 513); // exactly CRED_MAX_USERNAME_LENGTH

        await store.SetSecretAsync(atLimit, "value", TestToken);

        _nativeApi.Received(1).WriteGenericCredential(atLimit, "value");
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
        Assert.Throws<ArgumentNullException>(() => new WindowsCredentialManagerSecretStore(
            null!, _nativeApi, NullLogger<WindowsCredentialManagerSecretStore>.Instance));
        Assert.Throws<ArgumentNullException>(() => new WindowsCredentialManagerSecretStore(
            Options.Create(new SecretStoreOptions()), null!, NullLogger<WindowsCredentialManagerSecretStore>.Instance));
        Assert.Throws<ArgumentNullException>(() => new WindowsCredentialManagerSecretStore(
            Options.Create(new SecretStoreOptions()), _nativeApi, null!));
    }

    private WindowsCredentialManagerSecretStore CreateStore(SecretStoreOptions? options = null)
        => new(
            Options.Create(options ?? new SecretStoreOptions()),
            _nativeApi,
            NullLogger<WindowsCredentialManagerSecretStore>.Instance);
}
