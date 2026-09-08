// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SAF.Configuration.Secrets.Contracts;
using Xunit;

public class SecretStoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSecretStore_RegistersResolvableSecretStore()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSecretStore();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ISecretStore>());
    }

    [Fact]
    public void AddSecretStore_AppliesConfiguration()
    {
        var services = new ServiceCollection();

        services.AddSecretStore(o => o.Namespace = "custom");

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SecretStoreOptions>>().Value;
        Assert.Equal("custom", options.Namespace);
    }

    [Fact]
    public void AddSecretStore_DoesNotOverrideExistingSecretStore()
    {
        var services = new ServiceCollection();
        var custom = new StubSecretStore();
        services.AddSingleton<ISecretStore>(custom);

        services.AddSecretStore();

        using var provider = services.BuildServiceProvider();
        Assert.Same(custom, provider.GetRequiredService<ISecretStore>());
    }

    [Fact]
    public void AddWindowsCredentialManagerSecretStore_RegistersProvider_OnWindowsOnly()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        services.AddWindowsCredentialManagerSecretStore();

        using var provider = services.BuildServiceProvider();
        var providers = provider.GetServices<ISecretStoreProvider>().ToList();
        if (OperatingSystem.IsWindows())
        {
            Assert.Single(providers);
        }
        else
        {
            Assert.Empty(providers);
        }
    }

    [Fact]
    public void Extensions_Throw_OnNullServices()
    {
        Assert.Throws<ArgumentNullException>(() => SecretStoreServiceCollectionExtensions.AddSecretStore(null!));
        Assert.Throws<ArgumentNullException>(() => SecretStoreServiceCollectionExtensions.AddWindowsCredentialManagerSecretStore(null!));
    }

    private sealed class StubSecretStore : ISecretStore
    {
        public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveSecretAsync(string name, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
