// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SAF.Configuration.Secrets.Contracts;
using Xunit;

public class SecretResolvingConfigurationTests
{
    [Fact]
    public void ResolvesReferences_AndLeavesOtherValuesUntouched()
    {
        var config = Build(
            new Dictionary<string, string?>
            {
                ["Db:Password"] = "secret://app/db/pw",
                ["Db:Host"] = "localhost"
            },
            providers => providers.AddProvider<FakeReaderProvider>());

        Assert.Equal("resolved-pw", config["Db:Password"]);
        Assert.Equal("localhost", config["Db:Host"]);
    }

    [Fact]
    public void UnresolvableReference_BecomesNull()
    {
        var config = Build(
            new Dictionary<string, string?> { ["Db:Password"] = "secret://app/db/missing" },
            providers => providers.AddProvider<FakeReaderProvider>());

        Assert.Null(config["Db:Password"]);
    }

    [Fact]
    public void EnvironmentVariable_OverridesTheStore()
    {
        const string envVar = "SECRET__app__db__pw";
        Environment.SetEnvironmentVariable(envVar, "env-pw");
        try
        {
            var config = Build(
                new Dictionary<string, string?> { ["Db:Password"] = "secret://app/db/pw" },
                providers => providers.AddProvider<FakeReaderProvider>());

            Assert.Equal("env-pw", config["Db:Password"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, null);
        }
    }

    [Fact]
    public void NoReferences_DoesNotRequireAnyProvider()
    {
        var config = Build(
            new Dictionary<string, string?>
            {
                ["Db:Host"] = "localhost",
                ["Db:Port"] = "5432"
            },
            providers => { /* no providers registered */ });

        Assert.Equal("localhost", config["Db:Host"]);
        Assert.Equal("5432", config["Db:Port"]);
    }

    [Fact]
    public void AddResolvedSecrets_Throws_OnNullBuilder()
    {
        Assert.Throws<ArgumentNullException>(() => SecretConfigurationBuilderExtensions.AddResolvedSecrets(null!));
    }

    [Fact]
    public void ResolvesFromHostContainer_WhenHostServicesProvided()
    {
        var hostServices = new ServiceCollection()
            .AddLogging()
            .AddSecretStore()
            .AddProvider<HostProvider>()
            .Services
            .BuildServiceProvider();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Db:Password"] = "secret://app/db/pw" })
            .AddResolvedSecrets(hostServices)
            .Build();

        Assert.Equal("host:app/db/pw", config["Db:Password"]);
    }

    [Fact]
    public void ResolvesFromHostContainer_UsesOptionsConfiguredOnServiceCollection_NotAConfigureCallback()
    {
        // Regression test for N3: SecretStoreOptions set via services.Configure<SecretStoreOptions>(...)
        // (as AddSecretStore(o => ...) does) must reach the resolver even though AddResolvedSecrets itself
        // is called here with no configure callback of its own.
        var hostServices = new ServiceCollection()
            .AddLogging()
            .AddSecretStore(o => o.AllowEnvironmentOverride = false)
            .AddProvider<HostProvider>()
            .Services
            .BuildServiceProvider();

        const string envVar = "SECRET__app__db__pw";
        Environment.SetEnvironmentVariable(envVar, "should-be-ignored");
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Db:Password"] = "secret://app/db/pw" })
                .AddResolvedSecrets(hostServices)
                .Build();

            Assert.Equal("host:app/db/pw", config["Db:Password"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, null);
        }
    }

    private sealed class HostProvider : ISecretStoreProvider
    {
        public string Name => "host";

        public bool IsAvailable => true;

        public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>($"host:{name}");

        public Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveSecretAsync(string name, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private static IConfigurationRoot Build(
        Dictionary<string, string?> values,
        Action<ISecretStoreBuilder> configureProviders)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .AddResolvedSecrets(o => o.Namespace = "app", configureProviders)
            .Build();

    private sealed class FakeReaderProvider : ISecretStoreProvider
    {
        public string Name => "fake";

        public bool IsAvailable => true;

        public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(name == "app/db/pw" ? "resolved-pw" : null);

        public Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveSecretAsync(string name, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
