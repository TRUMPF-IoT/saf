// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAF.Configuration.Secrets.Configuration;
using SAF.Configuration.Secrets.Contracts;
using Xunit;

public sealed class SecretReferencePolicyTests : IDisposable
{
    private const string Reference = "secret://db/pw";

    private readonly StoreUse _store = new();
    private readonly CapturingLogger _logger = new();
    private readonly List<ServiceProvider> _hostServices = [];

    public void Dispose()
    {
        foreach (var hostServices in _hostServices)
        {
            hostServices.Dispose();
        }
    }

    [Fact]
    public void ResolvesAReferenceThePolicyAllows()
    {
        using var config = ResolveWithPolicy(new() { ["Db:Password"] = Reference });

        Assert.Equal("value:app/db/pw", config["Db:Password"]);
        Assert.Empty(_logger.Warnings);
    }

    [Fact]
    public void RefusedReference_ReadsAsNull_WithoutReadingTheStore()
    {
        using var config = ResolveWithPolicy(new() { ["Db:Password"] = Reference, ["Db:Host"] = Reference });

        Assert.Null(config["Db:Host"]);
        Assert.Contains(new KeyValuePair<string, string?>("Db:Host", null), config.AsEnumerable());
        Assert.Equal("value:app/db/pw", config["Db:Password"]);
        Assert.Equal(["app/db/pw"], _store.Requests);
    }

    [Fact]
    public void RefusedReference_IsLogged_WithItsKeyAndReference()
    {
        using var config = ResolveWithPolicy(new() { ["Db:Host"] = Reference });

        var warning = Assert.Single(_logger.Warnings);
        Assert.Contains("'Db:Host'", warning, StringComparison.Ordinal);
        Assert.Contains($"'{Reference}'", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyRefusedReferences_LeaveTheStoreUnbuilt()
    {
        using var config = ResolveWithPolicy(new() { ["Db:Host"] = Reference });

        Assert.Null(config["Db:Host"]);
        Assert.Equal(0, _store.ProvidersBuilt);
    }

    [Fact]
    public void WithoutAPolicy_ResolvesAReferenceInEveryKey()
    {
        using var config = Resolve(new() { ["Db:Host"] = Reference }, HostServices(policy: null));

        Assert.Equal("value:app/db/pw", config["Db:Host"]);
    }

    [Fact]
    public void AsksThePolicy_WithTheKeyAndTheReference()
    {
        var policy = new RecordingPolicy();
        using var config = Resolve(new() { ["Db:Password"] = Reference, ["Db:Host"] = "localhost" }, HostServices(policy));

        Assert.Equal([("Db:Password", Reference)], policy.Questions);
    }

    [Fact]
    public void RefusesAReferenceThatOnlyAppearsOnReload_AndLogsIt()
    {
        var inner = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Db:Host"] = "localhost" }).Build();
        using var config = (ConfigurationRoot)inner.ResolveSecrets(HostServices(Policy()));

        inner["Db:Host"] = Reference;
        inner.Reload();

        Assert.Null(config["Db:Host"]);
        Assert.Single(_logger.Warnings);
        Assert.Equal(0, _store.ProvidersBuilt);
    }

    [Fact]
    public void StandaloneResolution_HonoursAPolicyRegisteredWithTheProviders()
    {
        using var config = (ConfigurationRoot)new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Db:Password"] = Reference, ["Db:Host"] = Reference })
            .AddResolvedSecrets(o => o.Namespace = "app", providers =>
            {
                providers.Services.AddSingleton(_store);
                providers.AddProvider<RecordingProvider>();
                providers.Services.AddSecretReferencePolicy(Policy());
            })
            .Build();

        Assert.Equal("value:app/db/pw", config["Db:Password"]);
        Assert.Null(config["Db:Host"]);
    }

    // The later source answers before the null the resolver sets, so the consumer would read the literal
    // token as its value.
    [Fact]
    public void StandaloneResolution_ReportsARefusedReferenceFromALaterSource()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new ConfigurationBuilder()
            .AddResolvedSecrets(o => o.Namespace = "app", providers =>
            {
                providers.Services.AddSingleton(_store);
                providers.AddProvider<RecordingProvider>();
                providers.Services.AddSecretReferencePolicy(Policy());
            })
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Db:Host"] = Reference })
            .Build());

        Assert.Contains("'Db:Host'", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, _store.ProvidersBuilt);
    }

    [Fact]
    public void NamesSharingAnEnvironmentVariable_AreReadFromTheStore_AndLogged()
    {
        const string variable = "SECRET__app__shared__conn_1__pw";
        Environment.SetEnvironmentVariable(variable, "from-environment");
        try
        {
            using var config = Resolve(
                new() { ["Shared:Conn-1:Pw"] = "secret://shared/conn-1/pw", ["Shared:Conn_1:Pw"] = "secret://shared/conn_1/pw" },
                HostServices(new KeyBoundSecretReferencePolicy(), allowEnvironmentOverride: true));

            Assert.Equal("value:app/shared/conn-1/pw", config["Shared:Conn-1:Pw"]);
            Assert.Equal("value:app/shared/conn_1/pw", config["Shared:Conn_1:Pw"]);
            var warning = Assert.Single(_logger.Warnings);
            Assert.Contains($"'{variable}'", warning, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public void ANameWithAVariableOfItsOwn_IsReadFromTheEnvironment()
    {
        const string variable = "SECRET__app__own__conn_1__pw";
        Environment.SetEnvironmentVariable(variable, "from-environment");
        try
        {
            using var config = Resolve(
                new() { ["Own:Conn-1:Pw"] = "secret://own/conn-1/pw", ["Own:CONN-1:Pw2"] = "secret://OWN/CONN-1/PW" },
                HostServices(policy: null, allowEnvironmentOverride: true));

            Assert.Equal("from-environment", config["Own:Conn-1:Pw"]);
            Assert.Equal("from-environment", config["Own:CONN-1:Pw2"]);
            Assert.Empty(_logger.Warnings);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    private static SecretReferenceAllowList Policy() => new([new("Db:Password", "db/pw")]);

    private ConfigurationRoot ResolveWithPolicy(Dictionary<string, string?> values)
        => Resolve(values, HostServices(Policy()));

    private static ConfigurationRoot Resolve(Dictionary<string, string?> values, IServiceProvider hostServices)
        => (ConfigurationRoot)new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .AddResolvedSecrets(hostServices)
            .Build();

    private ServiceProvider HostServices(ISecretReferencePolicy? policy, bool allowEnvironmentOverride = false)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<ILogger<SecretResolvingConfigurationProvider>>(_logger)
            .AddSingleton(_store);
        services.AddSecretStore(o =>
        {
            o.Namespace = "app";
            o.AllowEnvironmentOverride = allowEnvironmentOverride;
        }).AddProvider<RecordingProvider>();
        if (policy is not null)
        {
            services.AddSecretReferencePolicy(policy);
        }

        var hostServices = services.BuildServiceProvider();
        _hostServices.Add(hostServices);
        return hostServices;
    }

    private sealed class StoreUse
    {
        public int ProvidersBuilt { get; set; }

        public List<string> Requests { get; } = [];
    }

    private sealed class RecordingProvider : ISecretStoreProvider
    {
        private readonly StoreUse _use;

        public RecordingProvider(StoreUse use)
        {
            _use = use;
            _use.ProvidersBuilt++;
        }

        public string Name => "recording";

        public bool IsAvailable => true;

        public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
        {
            _use.Requests.Add(name);
            return Task.FromResult<string?>($"value:{name}");
        }

        public Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveSecretAsync(string name, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class RecordingPolicy : ISecretReferencePolicy
    {
        public List<(string Key, string Reference)> Questions { get; } = [];

        public bool Allows(string key, SecretReference reference)
        {
            Questions.Add((key, reference.ToString()));
            return true;
        }
    }

    private sealed class CapturingLogger : ILogger<SecretResolvingConfigurationProvider>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }
}
