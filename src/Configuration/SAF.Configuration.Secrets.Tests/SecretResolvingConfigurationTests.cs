// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using System.IO.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAF.Configuration.Secrets.Contracts;
using Testably.Abstractions.Testing;
using Xunit;

public class SecretResolvingConfigurationTests
{
    private const string StorePath = "/store/secrets.json";

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

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
    public void UnresolvableReference_Throws_ByDefault()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(
            new Dictionary<string, string?> { ["Db:Password"] = "secret://app/db/missing" },
            providers => providers.AddProvider<FakeReaderProvider>()));

        Assert.Contains("secret://app/db/missing", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnresolvableReference_BecomesNull_WhenThrowOnUnresolvedReferenceIsFalse()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Db:Password"] = "secret://app/db/missing" })
            .AddResolvedSecrets(
                o =>
                {
                    o.Namespace = "app";
                    o.ThrowOnUnresolvedReference = false;
                },
                providers => providers.AddProvider<FakeReaderProvider>())
            .Build();

        Assert.Null(config["Db:Password"]);
    }

    [Fact]
    public void InitialLoad_PropagatesProviderException()
    {
        Assert.Throws<InvalidOperationException>(() => Build(
            new Dictionary<string, string?> { ["Db:Password"] = "secret://app/db/pw" },
            providers => providers.AddProvider<AlwaysThrowingProvider>()));
    }

    [Fact]
    public void Reload_ContainsProviderException_KeepsPreviousData_AndLogsWarning()
    {
        var reloadableSource = new ReloadableSource();
        var logger = new CapturingLogger<SecretResolvingConfigurationProvider>();
        var hostServices = new ServiceCollection()
            .AddLogging()
            .AddSingleton<ILogger<SecretResolvingConfigurationProvider>>(logger)
            .AddSecretStore(o => o.Namespace = "app")
            .AddProvider<FlakyProvider>()
            .Services
            .BuildServiceProvider();

        var config = new ConfigurationBuilder()
            .Add(reloadableSource)
            .AddResolvedSecrets(hostServices)
            .Build();

        Assert.Equal("resolved-pw", config["Db:Password"]);

        reloadableSource.Provider!.TriggerReload();

        Assert.Equal("resolved-pw", config["Db:Password"]);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Exception is InvalidOperationException);
    }

    [Fact]
    public void EnvironmentVariable_OverridesTheStore_WhenOverrideIsEnabled()
    {
        const string envVar = "SECRET__ns__app__db__pw";
        Environment.SetEnvironmentVariable(envVar, "env-pw");
        try
        {
            var config = BuildWithEnvironmentOverride();

            Assert.Equal("env-pw", config["Db:Password"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, null);
        }
    }

    [Fact]
    public void EnvironmentVariable_IsIgnored_ByDefault()
    {
        // AllowEnvironmentOverride defaults to false: enabling it widens the trust boundary to
        // everyone who can set this process's environment, so it must be opted into deliberately.
        const string envVar = "SECRET__ns__app__db__pw";
        Environment.SetEnvironmentVariable(envVar, "env-pw");
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Db:Password"] = "secret://app/db/pw" })
                .AddResolvedSecrets(o => o.Namespace = "ns", providers => providers.AddProvider<FakeReaderProvider>())
                .Build();

            Assert.Equal("resolved-pw", config["Db:Password"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, null);
        }
    }

    [Fact]
    public void EnvironmentVariableName_IncludesTheNamespace_SoHostsDoNotCollide()
    {
        // The variable is derived from the physical store key, not the bare reference name, so a
        // second host sharing this environment under a different namespace cannot override our secret.
        const string otherHostVar = "SECRET__other__app__db__pw";
        Environment.SetEnvironmentVariable(otherHostVar, "other-hosts-pw");
        try
        {
            var config = BuildWithEnvironmentOverride();

            Assert.Equal("resolved-pw", config["Db:Password"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(otherHostVar, null);
        }
    }

    [Fact]
    public void UnusableEnvironmentVariablePrefix_Throws_NamingTheOption()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Db:Password"] = "secret://app/db/pw" })
            .AddResolvedSecrets(
                o =>
                {
                    o.AllowEnvironmentOverride = true;
                    o.EnvironmentVariablePrefix = "not a valid prefix";
                },
                providers => providers.AddProvider<FakeReaderProvider>())
            .Build());

        Assert.Contains(nameof(SecretStoreOptions.EnvironmentVariablePrefix), ex.Message, StringComparison.Ordinal);
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
            .AddSecretStore(o => o.AllowEnvironmentOverride = true)
            .AddProvider<HostProvider>()
            .Services
            .BuildServiceProvider();

        // Asserted through a non-default option value: AllowEnvironmentOverride is false by default, so
        // the override only wins if the option really travelled from the service collection to the resolver.
        const string envVar = "SECRET__saf__app__db__pw";
        Environment.SetEnvironmentVariable(envVar, "from-environment");
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Db:Password"] = "secret://app/db/pw" })
                .AddResolvedSecrets(hostServices)
                .Build();

            Assert.Equal("from-environment", config["Db:Password"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, null);
        }
    }

    [Fact]
    public async Task Reference_CarriesTheLogicalNameOnly_AndTheStorePrependsTheNamespace()
    {
        // Pins the namespace convention end-to-end through a real store. The fake providers in this
        // file ignore Namespace entirely, so they cannot tell the two candidate designs apart — which
        // is how the docs came to show a reference that repeats the namespace.
        var fileSystem = new MockFileSystem();
        await using var hostServices = BuildFileStoreHost(fileSystem);

        await hostServices.GetRequiredService<ISecretStore>()
            .SetSecretAsync("opcua/conn-1/password", "s3cret", TestToken);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Opc:Password"] = "secret://opcua/conn-1/password"
            })
            .AddResolvedSecrets(hostServices)
            .Build();

        Assert.Equal("s3cret", config["Opc:Password"]);
        Assert.Contains(
            "myapp/opcua/conn-1/password",
            await fileSystem.File.ReadAllTextAsync(StorePath, TestToken),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reference_ThatRepeatsTheNamespace_DoesNotResolve()
    {
        var fileSystem = new MockFileSystem();
        await using var hostServices = BuildFileStoreHost(fileSystem);

        await hostServices.GetRequiredService<ISecretStore>()
            .SetSecretAsync("opcua/conn-1/password", "s3cret", TestToken);

        var ex = Assert.Throws<InvalidOperationException>(() => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Opc:Password"] = "secret://myapp/opcua/conn-1/password"
            })
            .AddResolvedSecrets(hostServices)
            .Build());

        Assert.Contains("secret://myapp/opcua/conn-1/password", ex.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildFileStoreHost(IFileSystem fileSystem)
        => new ServiceCollection()
            .AddLogging()
            .AddSingleton(fileSystem)
            .AddSingleton<ISecretProtector>(new ReversingSecretProtector())
            .AddSecretStore(o => o.Namespace = "myapp")
            .AddFile(o => o.Path = StorePath)
            .Services
            .BuildServiceProvider();

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

    private static IConfigurationRoot BuildWithEnvironmentOverride()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Db:Password"] = "secret://app/db/pw" })
            .AddResolvedSecrets(
                o =>
                {
                    o.Namespace = "ns";
                    o.AllowEnvironmentOverride = true;
                },
                providers => providers.AddProvider<FakeReaderProvider>())
            .Build();

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

    private sealed class AlwaysThrowingProvider : ISecretStoreProvider
    {
        public string Name => "always-throwing";

        public bool IsAvailable => true;

        public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The secret store is unavailable.");

        public Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveSecretAsync(string name, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    // Succeeds once (the initial Load()) and throws on every call after, simulating a store that becomes
    // unavailable between the startup resolution and a later reload.
    private sealed class FlakyProvider : ISecretStoreProvider
    {
        private int _callCount;

        public string Name => "flaky";

        public bool IsAvailable => true;

        public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
        {
            _callCount++;
            return _callCount == 1
                ? Task.FromResult<string?>("resolved-pw")
                : throw new InvalidOperationException("The secret store became unavailable.");
        }

        public Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveSecretAsync(string name, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    // A configuration source whose reload token can be fired on demand, so a test can trigger the
    // resolving provider's private Reload() without waiting on a real file watcher.
    private sealed class ReloadableSource : IConfigurationSource
    {
        public ReloadableProvider? Provider { get; private set; }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            Provider = new ReloadableProvider();
            return Provider;
        }
    }

    private sealed class ReloadableProvider : ConfigurationProvider
    {
        public override void Load() => Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Db:Password"] = "secret://app/db/pw"
        };

        public void TriggerReload() => OnReload();
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception), exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
