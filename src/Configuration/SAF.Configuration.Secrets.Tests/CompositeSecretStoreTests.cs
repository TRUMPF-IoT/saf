// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SAF.Configuration.Secrets.Contracts;
using Xunit;

public class CompositeSecretStoreTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Auto_SelectsFirstAvailableProvider()
    {
        var unavailable = MakeProvider("a", available: false);
        var available = MakeProvider("b", available: true);
        var store = CreateComposite(new SecretStoreOptions(), unavailable, available);

        await store.GetSecretAsync("k", TestToken);

        await available.Received(1).GetSecretAsync("k", Arg.Any<CancellationToken>());
        await unavailable.DidNotReceive().GetSecretAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Auto_Throws_WhenNoProviderIsAvailable()
    {
        var store = CreateComposite(new SecretStoreOptions(), MakeProvider("a", available: false));

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.GetSecretAsync("k", TestToken));
    }

    [Fact]
    public async Task Auto_Throws_WhenNoProvidersRegistered()
    {
        var store = CreateComposite(new SecretStoreOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.GetSecretAsync("k", TestToken));
    }

    [Fact]
    public async Task Named_SelectsMatchingProvider_CaseInsensitive()
    {
        var file = MakeProvider("file", available: true);
        var other = MakeProvider("windows-credential-manager", available: true);
        var store = CreateComposite(new SecretStoreOptions { ProviderName = "FILE" }, other, file);

        await store.GetSecretAsync("k", TestToken);

        await file.Received(1).GetSecretAsync("k", Arg.Any<CancellationToken>());
        await other.DidNotReceive().GetSecretAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Named_Throws_WhenProviderNotRegistered()
    {
        var store = CreateComposite(new SecretStoreOptions { ProviderName = "missing" }, MakeProvider("file", available: true));

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.GetSecretAsync("k", TestToken));
    }

    [Fact]
    public async Task Named_Throws_WhenProviderNotAvailable()
    {
        var store = CreateComposite(new SecretStoreOptions { ProviderName = "file" }, MakeProvider("file", available: false));

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.GetSecretAsync("k", TestToken));
    }

    [Fact]
    public async Task DelegatesReadWriteDelete_ToSelectedProvider()
    {
        var provider = MakeProvider("file", available: true);
        provider.GetSecretAsync("k", Arg.Any<CancellationToken>()).Returns(Task.FromResult<string?>("v"));
        var store = CreateComposite(new SecretStoreOptions { ProviderName = "file" }, provider);

        var read = await store.GetSecretAsync("k", TestToken);
        await store.SetSecretAsync("k", "v", TestToken);
        await store.RemoveSecretAsync("k", TestToken);

        Assert.Equal("v", read);
        await provider.Received(1).SetSecretAsync("k", "v", Arg.Any<CancellationToken>());
        await provider.Received(1).RemoveSecretAsync("k", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectsProviderOnce_ForRepeatedCalls()
    {
        var provider = MakeProvider("file", available: true);
        var store = CreateComposite(new SecretStoreOptions { ProviderName = "file" }, provider);

        await store.GetSecretAsync("k", TestToken);
        await store.GetSecretAsync("k", TestToken);

        // IsAvailable is only evaluated during the single (cached) selection, not per call.
        _ = provider.Received(1).IsAvailable;
    }

    [Fact]
    public void Constructor_Throws_OnNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new CompositeSecretStore(
            null!, Options.Create(new SecretStoreOptions()), NullLogger<CompositeSecretStore>.Instance));
        Assert.Throws<ArgumentNullException>(() => new CompositeSecretStore(
            [], null!, NullLogger<CompositeSecretStore>.Instance));
        Assert.Throws<ArgumentNullException>(() => new CompositeSecretStore(
            [], Options.Create(new SecretStoreOptions()), null!));
    }

    private static ISecretStoreProvider MakeProvider(string name, bool available)
    {
        var provider = Substitute.For<ISecretStoreProvider>();
        provider.Name.Returns(name);
        provider.IsAvailable.Returns(available);
        return provider;
    }

    private static CompositeSecretStore CreateComposite(SecretStoreOptions options, params ISecretStoreProvider[] providers)
        => new(providers, Options.Create(options), NullLogger<CompositeSecretStore>.Instance);
}
