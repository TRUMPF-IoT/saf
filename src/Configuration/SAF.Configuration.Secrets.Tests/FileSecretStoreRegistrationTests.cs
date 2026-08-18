// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using System.IO.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SAF.Configuration.Secrets.Contracts;
using SAF.Configuration.Secrets.FileStore;
using Testably.Abstractions.Testing;
using Xunit;

public class FileSecretStoreRegistrationTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public void AddFile_RegistersFileProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISecretProtector>(new ReversingSecretProtector());

        services.AddSecretStore().AddFile();

        using var sp = services.BuildServiceProvider();
        var providers = sp.GetServices<ISecretStoreProvider>().ToList();
        Assert.Single(providers);
        Assert.IsType<FileSecretStore>(providers[0]);
    }

    [Fact]
    public void AddFile_AppendsFileProviderAfterNativeStore()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISecretProtector>(new ReversingSecretProtector());

        services.AddSecretStore().AddDefaults().AddFile();

        using var sp = services.BuildServiceProvider();
        var providers = sp.GetServices<ISecretStoreProvider>().ToList();
        // The file store is always last so an OS-native store wins auto-selection where available.
        Assert.IsType<FileSecretStore>(providers[^1]);
    }

    [Fact]
    public void AddFileSecretStore_BindsOptions()
    {
        var services = new ServiceCollection();

        services.AddFileSecretStore(o =>
        {
            o.Path = "/x/secrets.json";
            o.ReaderPrincipal = "NT SERVICE\\QDS-2";
        });

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<FileSecretStoreOptions>>().Value;
        Assert.Equal("/x/secrets.json", options.Path);
        Assert.Equal("NT SERVICE\\QDS-2", options.ReaderPrincipal);
    }

    [Fact]
    public void AddFileSecretStore_RegistersDefaultFileSystem_WhenNoneRegistered()
    {
        var services = new ServiceCollection();

        services.AddFileSecretStore();

        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<IFileSystem>());
    }

    [Fact]
    public void AddFileSecretStore_DoesNotOverrideExistingFileSystem()
    {
        var services = new ServiceCollection();
        var fileSystem = new MockFileSystem();
        services.AddSingleton<IFileSystem>(fileSystem);

        services.AddFileSecretStore();

        using var sp = services.BuildServiceProvider();
        Assert.Same(fileSystem, sp.GetRequiredService<IFileSystem>());
    }

    [Fact]
    public void AddFile_IsIdempotent()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISecretProtector>(new ReversingSecretProtector());

        services.AddSecretStore().AddFile().AddFile();

        using var sp = services.BuildServiceProvider();
        Assert.Single(sp.GetServices<ISecretStoreProvider>());
    }

    [Fact]
    public async Task ResolvedStore_RoundTripsThroughFileProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IFileSystem>(new MockFileSystem());
        services.AddSingleton<ISecretProtector>(new ReversingSecretProtector());
        services.AddSecretStore(o => o.ProviderName = FileSecretStore.ProviderName)
            .AddFile(o => o.Path = "/data/secrets.json");

        using var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<ISecretStore>();

        await store.SetSecretAsync("conn/pw", "value", TestToken);

        Assert.Equal("value", await store.GetSecretAsync("conn/pw", TestToken));
    }

    [Fact]
    public async Task NamedFileStore_WithoutProtector_ReportsUnavailable_OnUse()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IFileSystem>(new MockFileSystem());
        services.AddSecretStore(o => o.ProviderName = FileSecretStore.ProviderName).AddFile();

        using var sp = services.BuildServiceProvider();

        // The store now constructs without a protector, so resolution succeeds; selection then reports a
        // clear "not available" error instead of the obscure DI failure the eager construction used to raise.
        var store = sp.GetRequiredService<ISecretStore>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await store.GetSecretAsync("k", TestToken));
        Assert.Contains("not available", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AutoSelection_WithoutProtector_ReportsNoAvailableProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IFileSystem>(new MockFileSystem());
        // Mirrors AddDefaults() on a non-Windows host: only the file store is registered, and without a
        // protector it stays unavailable, so auto-selection fails clearly rather than at construction.
        services.AddSecretStore().AddFile();

        using var sp = services.BuildServiceProvider();

        var store = sp.GetRequiredService<ISecretStore>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await store.GetSecretAsync("k", TestToken));
        Assert.Contains("no available secret store provider", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddFile_Throws_OnNullBuilder()
        => Assert.Throws<ArgumentNullException>(() => SecretStoreBuilderExtensions.AddFile(null!));

    [Fact]
    public void AddFileSecretStore_Throws_OnNullServices()
        => Assert.Throws<ArgumentNullException>(() => SecretStoreServiceCollectionExtensions.AddFileSecretStore(null!));
}
