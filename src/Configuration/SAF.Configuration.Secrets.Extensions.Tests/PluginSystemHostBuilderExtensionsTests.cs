// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Extensions.Tests;

using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SAF.Configuration.Secrets;
using SAF.Configuration.Secrets.Contracts;
using SAF.PluginSystem.Hosting.Contracts;
using Xunit;

public class PluginSystemHostBuilderExtensionsTests
{
    [Fact]
    public void AddSecretStore_RegistersStoreAndForwarder_AndReturnsBuilder()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var hostBuilder = Substitute.For<IPluginSystemHostBuilder>();
        hostBuilder.Services.Returns(services);

        var result = hostBuilder.AddSecretStore();

        Assert.Same(hostBuilder, result);
        Assert.Contains(services, d => d.ServiceType == typeof(IHostServiceForwarder));

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ISecretStore>());
    }

    [Fact]
    public void AddSecretStore_AppliesConfiguration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var hostBuilder = Substitute.For<IPluginSystemHostBuilder>();
        hostBuilder.Services.Returns(services);

        hostBuilder.AddSecretStore(o => o.ProviderName = "file");

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SecretStoreOptions>>().Value;
        Assert.Equal("file", options.ProviderName);
    }

    [Fact]
    public void AddSecretStore_WithExplicitProviders_RegistersOnlyThose()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var hostBuilder = Substitute.For<IPluginSystemHostBuilder>();
        hostBuilder.Services.Returns(services);

        hostBuilder.AddSecretStore(configureProviders: providers => providers.AddProvider<StubProvider>());

        using var provider = services.BuildServiceProvider();
        var registered = provider.GetServices<ISecretStoreProvider>().ToList();
        Assert.Single(registered);
        Assert.IsType<StubProvider>(registered[0]);
    }

    [Fact]
    public void AddSecretStore_Throws_OnNullBuilder()
    {
        Assert.Throws<ArgumentNullException>(() => PluginSystemHostBuilderExtensions.AddSecretStore(null!));
    }

    [Fact]
    public void AddSecretConfigurationResolution_ReturnsBuilderForChaining()
    {
        var services = new ServiceCollection();
        var hostBuilder = Substitute.For<IPluginSystemHostBuilder>();
        hostBuilder.Services.Returns(services);

        var result = hostBuilder.AddSecretConfigurationResolution();

        Assert.Same(hostBuilder, result);
    }

    [Fact]
    public void AddSecretConfigurationResolution_Throws_OnNullBuilder()
    {
        Assert.Throws<ArgumentNullException>(
            () => PluginSystemHostBuilderExtensions.AddSecretConfigurationResolution(null!));
    }

    private sealed class StubProvider : ISecretStoreProvider
    {
        public string Name => "stub";

        public bool IsAvailable => true;

        public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveSecretAsync(string name, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
