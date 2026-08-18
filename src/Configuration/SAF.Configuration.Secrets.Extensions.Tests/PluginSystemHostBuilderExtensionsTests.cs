// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Extensions.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using SAF.Configuration.Secrets;
using SAF.Configuration.Secrets.Contracts;
using SAF.Configuration.Secrets.Extensions;
using SAF.PluginSystem.Hosting;
using SAF.PluginSystem.Hosting.Contracts;
using Xunit;
using PluginSystemHostBuilderExtensions = SAF.Configuration.Secrets.Extensions.PluginSystemHostBuilderExtensions;

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

    [Fact]
    public void AddSecretConfigurationResolution_ResolvesSecretInPluginConfiguration_ThroughRealHost()
    {
        // Regression test for N2: the secret must already be resolved by the time IPluginSystemHostContext
        // .PluginConfiguration is built, since that is also what a plugin manifest's ConfigureServices reads
        // -- there is no later phase where resolution could still catch up. Goes through a real
        // HostApplicationBuilder/AddPluginSystem/IHost, not a substituted builder, so it also proves
        // AddPluginConfigurationSource + AddSecretConfigurationResolution actually compose end to end.
        var builder = Host.CreateApplicationBuilder();
        var pluginSystemBuilder = builder.AddPluginSystem(_ => { });

        pluginSystemBuilder.AddPluginConfigurationSource(source =>
            source.Builder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MyPlugin:Password"] = "secret://db/pw",
            }));
        pluginSystemBuilder.AddSecretConfigurationResolution(
            configureProviders: providers => providers.AddProvider<FakeSecretProvider>());

        using var host = builder.Build();
        var context = host.Services.GetRequiredService<IPluginSystemHostContext>();

        Assert.Equal("resolved:db/pw", context.PluginConfiguration["MyPlugin:Password"]);
    }

    private sealed class FakeSecretProvider : ISecretStoreProvider
    {
        public string Name => "fake";

        public bool IsAvailable => true;

        public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>($"resolved:{name}");

        public Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveSecretAsync(string name, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
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
