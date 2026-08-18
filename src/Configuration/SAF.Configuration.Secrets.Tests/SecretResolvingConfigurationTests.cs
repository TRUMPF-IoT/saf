// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
