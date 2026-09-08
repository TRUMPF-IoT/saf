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

        Assert.Equal("resolved:saf/db/pw", context.PluginConfiguration["MyPlugin:Password"]);
    }

    [Fact]
    public void AddSecretConfigurationResolution_ResolvesASourceRegisteredAfterIt()
    {
        // Regression test for N4: the callback registration order used to decide whether a plugin received
        // its credential or the literal secret:// token, because the sources to resolve were captured when
        // resolution was registered instead of after every callback had run.
        var builder = Host.CreateApplicationBuilder();
        var pluginSystemBuilder = builder.AddPluginSystem(_ => { });

        pluginSystemBuilder.AddSecretConfigurationResolution(
            configureProviders: providers => providers.AddProvider<FakeSecretProvider>());
        pluginSystemBuilder.AddPluginConfigurationSource(source =>
            source.Builder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MyPlugin:Password"] = "secret://db/pw",
            }));

        using var host = builder.Build();
        var context = host.Services.GetRequiredService<IPluginSystemHostContext>();

        Assert.Equal("resolved:saf/db/pw", context.PluginConfiguration["MyPlugin:Password"]);
    }

    [Fact]
    public void AddSecretConfigurationResolution_BuildsEachConfigurationSourceOnce()
    {
        // The resolver chains the root the host already built instead of rebuilding the sources, so each
        // settings file is parsed once and watched by one file watcher, not two.
        var countingSource = new CountingSource();
        var builder = Host.CreateApplicationBuilder();
        var pluginSystemBuilder = builder.AddPluginSystem(_ => { });

        pluginSystemBuilder.AddPluginConfigurationSource(source => source.Builder.Add(countingSource));
        pluginSystemBuilder.AddSecretConfigurationResolution(
            configureProviders: providers => providers.AddProvider<FakeSecretProvider>());

        using var host = builder.Build();
        var context = host.Services.GetRequiredService<IPluginSystemHostContext>();

        Assert.Equal("resolved:saf/db/pw", context.PluginConfiguration["MyPlugin:Password"]);
        Assert.Equal(1, countingSource.BuildCount);
    }

    [Fact]
    public void AddSecretConfigurationResolution_KeepsTheProvidersAnEarlierCallRegistered()
    {
        // The order providers are registered in is the order auto-selection picks from, and TryAddEnumerable
        // appends: adding the platform defaults on top here could not override the explicit choice, it would
        // only add a fallback backend nobody asked for.
        var services = NewServices();
        var hostBuilder = NewHostBuilder(services);

        hostBuilder.AddSecretStore(configureProviders: providers => providers.AddProvider<StubProvider>());
        hostBuilder.AddSecretConfigurationResolution();

        using var provider = services.BuildServiceProvider();
        var registered = provider.GetServices<ISecretStoreProvider>().ToList();
        Assert.Single(registered);
        Assert.IsType<StubProvider>(registered[0]);
    }

    [Fact]
    public void AddSecretConfigurationResolution_KeepsProvidersRegisteredOutsideTheseExtensions()
    {
        var services = NewServices();
        services.AddSecretStore().AddProvider<StubProvider>();
        var hostBuilder = NewHostBuilder(services);

        hostBuilder.AddSecretConfigurationResolution();

        using var provider = services.BuildServiceProvider();
        Assert.IsType<StubProvider>(Assert.Single(provider.GetServices<ISecretStoreProvider>()));
    }

    [Fact]
    public void AddSecretStore_Throws_WhenProvidersWereAlreadyConfiguredByResolution()
    {
        // The reported failure: on Windows this used to leave the Credential Manager selected and ignore the
        // explicit AddFile(), so secrets provisioned into the encrypted file were never read.
        var services = NewServices();
        var hostBuilder = NewHostBuilder(services);
        hostBuilder.AddSecretConfigurationResolution();

        var exception = Assert.Throws<InvalidOperationException>(
            () => hostBuilder.AddSecretStore(configureProviders: providers => providers.AddProvider<StubProvider>()));

        Assert.Contains(nameof(PluginSystemHostBuilderExtensions.AddSecretConfigurationResolution), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(PluginSystemHostBuilderExtensions.AddSecretStore), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddSecretConfigurationResolution_Throws_WhenProvidersWereAlreadyConfiguredByTheStore()
    {
        var services = NewServices();
        var hostBuilder = NewHostBuilder(services);
        hostBuilder.AddSecretStore(configureProviders: providers => providers.AddProvider<StubProvider>());

        Assert.Throws<InvalidOperationException>(
            () => hostBuilder.AddSecretConfigurationResolution(
                configureProviders: providers => providers.AddProvider<FakeSecretProvider>()));
    }

    [Fact]
    public void AddSecretStore_ForwardsTheStoreOnce_WhenCalledTwice()
    {
        var services = NewServices();
        var hostBuilder = NewHostBuilder(services);

        hostBuilder.AddSecretStore();
        hostBuilder.AddSecretStore();

        Assert.Single(services, d => d.ServiceType == typeof(IHostServiceForwarder));
    }

    [Fact]
    public void AddSecretConfigurationResolution_RegistersNothingNew_WhenCalledTwice()
    {
        // A second root decorator would resolve the already resolved configuration again, with its own
        // resolver, its own change-token registration and its own reader.
        var services = NewServices();
        var hostBuilder = NewHostBuilder(services);
        hostBuilder.AddSecretConfigurationResolution();
        var registeredAfterFirstCall = services.Count;

        hostBuilder.AddSecretConfigurationResolution();

        Assert.Equal(registeredAfterFirstCall, services.Count);
    }

    [Fact]
    public void AddSecretStore_AndAddSecretConfigurationResolution_ApplyBothOptionsCallbacks_InCallOrder()
    {
        var services = NewServices();
        var hostBuilder = NewHostBuilder(services);

        hostBuilder.AddSecretStore(o =>
        {
            o.Namespace = "first";
            o.ProviderName = "first";
        });
        hostBuilder.AddSecretConfigurationResolution(o => o.ProviderName = "second");

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SecretStoreOptions>>().Value;
        Assert.Equal("first", options.Namespace);
        Assert.Equal("second", options.ProviderName);
    }

    private static ServiceCollection NewServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    private static IPluginSystemHostBuilder NewHostBuilder(IServiceCollection services)
    {
        var hostBuilder = Substitute.For<IPluginSystemHostBuilder>();
        hostBuilder.Services.Returns(services);
        return hostBuilder;
    }

    private sealed class CountingSource : IConfigurationSource
    {
        public int BuildCount { get; private set; }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            BuildCount++;
            return new CountingProvider();
        }

        private sealed class CountingProvider : ConfigurationProvider
        {
            public override void Load() => Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["MyPlugin:Password"] = "secret://db/pw"
            };
        }
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
