// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using NSubstitute;
using SAF.Configuration.Secrets.WindowsCredentialManager;
using Xunit;

public class WindowsCredentialManagerSecretStoreTests
{
    // The composite store hands a provider the finished target name, so these tests pass one in
    // directly - a provider that namespaced again would turn it into "saf/saf/conn/pw".
    private const string TargetName = "saf/conn/pw";

    private readonly INativeCredentialApi _nativeApi = Substitute.For<INativeCredentialApi>();

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetSecretAsync_ReturnsValue_WhenCredentialExists()
    {
        _nativeApi
            .TryReadGenericCredential(TargetName, out Arg.Any<string?>())
            .Returns(call => { call[1] = "s3cr3t"; return true; });
        var store = CreateStore();

        var result = await store.GetSecretAsync(TargetName, TestToken);

        Assert.Equal("s3cr3t", result);
    }

    [Fact]
    public async Task GetSecretAsync_ReturnsNull_WhenCredentialMissing()
    {
        _nativeApi
            .TryReadGenericCredential(Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(false);
        var store = CreateStore();

        var result = await store.GetSecretAsync(TargetName, TestToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task SetSecretAsync_WritesTheTargetNameVerbatim()
    {
        var store = CreateStore();

        await store.SetSecretAsync(TargetName, "value", TestToken);

        _nativeApi.Received(1).WriteGenericCredential(TargetName, "value");
    }

    [Fact]
    public async Task RemoveSecretAsync_DeletesTheTargetNameVerbatim()
    {
        var store = CreateStore();

        await store.RemoveSecretAsync(TargetName, TestToken);

        _nativeApi.Received(1).DeleteGenericCredential(TargetName);
    }

    [Fact]
    public async Task RemoveSecretAsync_DoesNotThrow_WhenCredentialMissing()
    {
        _nativeApi.DeleteGenericCredential(Arg.Any<string>()).Returns(false);
        var store = CreateStore();

        await store.RemoveSecretAsync(TargetName, TestToken);

        _nativeApi.Received(1).DeleteGenericCredential(TargetName);
    }

    [Fact]
    public void Name_IsStableProviderIdentifier()
    {
        Assert.Equal("windows-credential-manager", CreateStore().Name);
        Assert.Equal(WindowsCredentialManagerSecretStore.ProviderName, CreateStore().Name);
    }

    [Fact]
    public void IsAvailable_OnlyOnWindows()
    {
        Assert.Equal(OperatingSystem.IsWindows(), CreateStore().IsAvailable);
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
            async () => await store.SetSecretAsync(TargetName, null!, TestToken));
    }

    [Fact]
    public async Task SetSecretAsync_Throws_WhenValueExceedsBlobSizeLimit()
    {
        var store = CreateStore();
        var tooLong = new string('a', 1281); // 2562 bytes UTF-16, over the 2560-byte CRED_MAX_CREDENTIAL_BLOB_SIZE

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await store.SetSecretAsync(TargetName, tooLong, TestToken));
        _nativeApi.DidNotReceive().WriteGenericCredential(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SetSecretAsync_Writes_WhenValueIsAtBlobSizeLimit()
    {
        var store = CreateStore();
        var atLimit = new string('a', 1280); // exactly 2560 bytes UTF-16

        await store.SetSecretAsync(TargetName, atLimit, TestToken);

        _nativeApi.Received(1).WriteGenericCredential(TargetName, atLimit);
    }

    [Fact]
    public async Task SetSecretAsync_Throws_WhenTargetNameExceedsUsernameLimit()
    {
        var store = CreateStore();
        var tooLong = new string('a', 514); // over the 513-char CRED_MAX_USERNAME_LENGTH

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await store.SetSecretAsync(tooLong, "value", TestToken));
        _nativeApi.DidNotReceive().WriteGenericCredential(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SetSecretAsync_Writes_WhenTargetNameIsAtUsernameLimit()
    {
        var store = CreateStore();
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
            async () => await store.GetSecretAsync(TargetName, cts.Token));
    }

    [Fact]
    public void Constructor_Throws_OnNullDependency()
    {
        Assert.Throws<ArgumentNullException>(() => new WindowsCredentialManagerSecretStore(null!));
    }

    private WindowsCredentialManagerSecretStore CreateStore() => new(_nativeApi);
}
