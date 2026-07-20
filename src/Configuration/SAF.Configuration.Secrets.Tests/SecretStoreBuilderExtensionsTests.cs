// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using Microsoft.Extensions.DependencyInjection;
using SAF.Configuration.Secrets.Contracts;
using SAF.Configuration.Secrets.WindowsCredentialManager;
using Xunit;

public class SecretStoreBuilderExtensionsTests
{
    [Fact]
    public void AddProvider_RegistersProvidersInCallOrder()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSecretStore()
            .AddProvider<StubProviderA>()
            .AddProvider<StubProviderB>();

        using var provider = services.BuildServiceProvider();
        var providers = provider.GetServices<ISecretStoreProvider>().ToList();
        Assert.Collection(
            providers,
            p => Assert.IsType<StubProviderA>(p),
            p => Assert.IsType<StubProviderB>(p));
    }

    [Fact]
    public void AddDefaults_RegistersWindowsProvider_OnWindowsOnly()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSecretStore().AddDefaults();

        using var provider = services.BuildServiceProvider();
        var providers = provider.GetServices<ISecretStoreProvider>().ToList();
        if (OperatingSystem.IsWindows())
        {
            Assert.Single(providers);
            Assert.IsType<WindowsCredentialManagerSecretStore>(providers[0]);
        }
        else
        {
            Assert.Empty(providers);
        }
    }

    [Fact]
    public void BuilderExtensions_Throw_OnNullBuilder()
    {
        Assert.Throws<ArgumentNullException>(() => SecretStoreBuilderExtensions.AddDefaults(null!));
        Assert.Throws<ArgumentNullException>(() => SecretStoreBuilderExtensions.AddWindowsCredentialManager(null!));
        Assert.Throws<ArgumentNullException>(() => SecretStoreBuilderExtensions.AddProvider<StubProviderA>(null!));
    }

    private abstract class StubProvider : ISecretStoreProvider
    {
        public abstract string Name { get; }

        public bool IsAvailable => true;

        public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveSecretAsync(string name, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubProviderA : StubProvider
    {
        public override string Name => "stub-a";
    }

    private sealed class StubProviderB : StubProvider
    {
        public override string Name => "stub-b";
    }
}
