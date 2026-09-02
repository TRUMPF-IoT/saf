// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using System.IO.Abstractions;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SAF.Configuration.Secrets.Configuration;
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

    [Fact]
    public void ReferenceFromASourceAddedAfterTheCall_Throws_InsteadOfHandingOutTheLiteralToken()
    {
        // The later source answers TryGet first, so its unresolved reference would reach the consumer as
        // the credential. Failing loudly is the point: a silent secret:// string is fail-open.
        var ex = Assert.Throws<InvalidOperationException>(() => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Db:Host"] = "localhost" })
            .AddResolvedSecrets(o => o.Namespace = "app", providers => providers.AddProvider<FakeReaderProvider>())
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Db:Password"] = "secret://app/db/pw" })
            .Build());

        Assert.Contains("Db:Password", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlainValueFromASourceAddedAfterTheCall_OverridesAnEarlierReference()
    {
        // The sources are read at Build() time, so the resolver sees the final composed value: the later
        // plain override wins and nothing is resolved for that key.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Db:Password"] = "secret://app/db/pw" })
            .AddResolvedSecrets(o => o.Namespace = "app", providers => providers.AddProvider<FakeReaderProvider>())
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Db:Password"] = "plain" })
            .Build();

        Assert.Equal("plain", config["Db:Password"]);
    }

    [Fact]
    public void TwoResolvingSources_DoNotBuildEachOther()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Db:Password"] = "secret://app/db/pw" })
            .AddResolvedSecrets(o => o.Namespace = "app", providers => providers.AddProvider<FakeReaderProvider>())
            .AddResolvedSecrets(o => o.Namespace = "app", providers => providers.AddProvider<FakeReaderProvider>())
            .Build();

        Assert.Equal("resolved-pw", config["Db:Password"]);
    }

    [Fact]
    public void InnerBuilder_InheritsTheBuilderProperties()
    {
        // The properties carry the builder's default file provider. Without them a source built by the
        // resolver before the outer builder reaches it falls back to FileConfigurationSource
        // .EnsureDefaults(), creating an undisposed PhysicalFileProvider over the base directory - a
        // recursive file watcher nothing owns.
        var marker = new object();
        var laterSource = new PropertyCapturingSource();
        var builder = new ConfigurationBuilder();
        builder.Properties["test-marker"] = marker;

        builder
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Db:Host"] = "localhost" })
            .AddResolvedSecrets(o => o.Namespace = "app", providers => providers.AddProvider<FakeReaderProvider>())
            .Add(laterSource)
            .Build();

        // The resolver builds it first, so the first capture is the one that proves the properties came along.
        Assert.Same(marker, laterSource.CapturedProperties[0]);
    }

    [Fact]
    public void SameReference_FromSeveralKeys_IsResolvedOnce()
    {
        var provider = new CountingProvider();
        var hostServices = new ServiceCollection()
            .AddLogging()
            .AddSecretStore(o => o.Namespace = "app")
            .Services
            .AddSingleton<ISecretStoreProvider>(provider)
            .BuildServiceProvider();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Db:Password"] = "secret://app/db/pw",
                ["Cache:Password"] = "secret://app/db/pw"
            })
            .AddResolvedSecrets(hostServices)
            .Build();

        Assert.Equal("resolved-pw", config["Db:Password"]);
        Assert.Equal("resolved-pw", config["Cache:Password"]);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public void Resolve_TimesOut_WhenTheStoreNeverAnswers()
    {
        var hostServices = new ServiceCollection()
            .AddLogging()
            .AddSecretStore(o =>
            {
                o.Namespace = "app";
                o.ResolveTimeout = TimeSpan.FromMilliseconds(50);
            })
            .AddProvider<NeverAnsweringProvider>()
            .Services
            .BuildServiceProvider();

        var ex = Assert.Throws<TimeoutException>(() => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Db:Password"] = "secret://app/db/pw" })
            .AddResolvedSecrets(hostServices)
            .Build());

        Assert.Contains(nameof(SecretStoreOptions.ResolveTimeout), ex.Message, StringComparison.Ordinal);
    }


    [Fact]
    public void Resolve_TimesOut_WhenTheStoreIgnoresTheCancellationToken()
    {
        // The in-box Windows provider blocks in the synchronous CredReadW P/Invoke and any provider added
        // through AddProvider<T> may do the same, so the timeout must not depend on the token being observed.
        var hostServices = new ServiceCollection()
            .AddLogging()
            .AddSecretStore(o =>
            {
                o.Namespace = "app";
                o.ResolveTimeout = TimeSpan.FromMilliseconds(100);
            })
            .AddProvider<BlockingProvider>()
            .Services
            .BuildServiceProvider();

        var elapsed = Stopwatch.StartNew();
        var ex = Assert.Throws<TimeoutException>(() => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Db:Password"] = "secret://app/db/pw" })
            .AddResolvedSecrets(hostServices)
            .Build());
        elapsed.Stop();

        Assert.Contains(nameof(SecretStoreOptions.ResolveTimeout), ex.Message, StringComparison.Ordinal);
        Assert.True(
            elapsed.Elapsed < BlockingProvider.BlockFor,
            $"the timeout must abandon the blocked store, but the build took {elapsed.Elapsed}");
    }
    [Fact]
    public void NonPositiveResolveTimeout_Throws_NamingTheOption()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Db:Password"] = "secret://app/db/pw" })
            .AddResolvedSecrets(
                o =>
                {
                    o.Namespace = "app";
                    o.ResolveTimeout = TimeSpan.Zero;
                },
                providers => providers.AddProvider<FakeReaderProvider>())
            .Build());

        Assert.Contains(nameof(SecretStoreOptions.ResolveTimeout), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InfiniteResolveTimeout_IsAccepted()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Db:Password"] = "secret://app/db/pw" })
            .AddResolvedSecrets(
                o =>
                {
                    o.Namespace = "app";
                    o.ResolveTimeout = Timeout.InfiniteTimeSpan;
                },
                providers => providers.AddProvider<FakeReaderProvider>())
            .Build();

        Assert.Equal("resolved-pw", config["Db:Password"]);
    }

    [Fact]
    public void AddResolvedSecrets_Throws_OnAnEagerlyBuildingBuilder()
    {
        // ConfigurationManager - Host.CreateApplicationBuilder().Configuration - builds each source as it
        // is added, so the resolver never sees the sources that follow and used to hand the consumer the
        // literal secret:// token as its credential, silently and with no shadowing error.
        using var manager = new ConfigurationManager();
        ((IConfigurationBuilder)manager).AddInMemoryCollection(
            new Dictionary<string, string?> { ["Db:Password"] = "secret://app/db/pw" });

        var ex = Assert.Throws<NotSupportedException>(() => ((IConfigurationBuilder)manager).AddResolvedSecrets(
            o => o.Namespace = "app",
            providers => providers.AddProvider<FakeReaderProvider>()));

        Assert.Contains(nameof(ConfigurationManager), ex.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ConfigurationBuilder), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddResolvedSecrets_Throws_WithoutRegisteringTheSource()
    {
        using var manager = new ConfigurationManager();
        var sourcesBefore = ((IConfigurationBuilder)manager).Sources.ToList();

        Assert.Throws<NotSupportedException>(
            () => ((IConfigurationBuilder)manager).AddResolvedSecrets());

        // Refused before Add, so the eager builder never got to build a half-blind resolver.
        Assert.Equal(sourcesBefore, ((IConfigurationBuilder)manager).Sources);
    }

    [Fact]
    public void AddResolvedSecrets_ResolvesThroughAnEagerBuilder_WhenTheRootIsComposedSeparately()
    {
        // The shape the exception points at: compose and resolve in a ConfigurationBuilder, then chain the
        // built root into the eager one.
        using var manager = new ConfigurationManager();
        var resolved = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Db:Password"] = "secret://app/db/pw" })
            .AddResolvedSecrets(
                o => o.Namespace = "app",
                providers => providers.AddProvider<FakeReaderProvider>())
            .Build();
        ((IConfigurationBuilder)manager).AddConfiguration(resolved);

        Assert.Equal("resolved-pw", manager["Db:Password"]);
    }
    [Fact]
    public void ChainedRoot_ResolvesReferences_WithoutRebuildingTheSources()
    {
        var countingSource = new CountingSource(new Dictionary<string, string?>
        {
            ["Db:Password"] = "secret://app/db/pw",
            ["Db:Host"] = "localhost"
        });

        var innerRoot = new ConfigurationBuilder().Add(countingSource).Build();
        var config = innerRoot.ResolveSecrets(
            hostServices: null,
            o => o.Namespace = "app",
            providers => providers.AddProvider<FakeReaderProvider>());

        Assert.Equal("resolved-pw", config["Db:Password"]);
        Assert.Equal("localhost", config["Db:Host"]);

        // The already built root is chained, so each source is built - and each file therefore parsed and
        // watched - once, instead of once for the host and once more for the resolver.
        Assert.Equal(1, countingSource.BuildCount);
    }


    [Fact]
    public void ChainedRoot_KeepsAConfiguredEmptyValueEmpty_RatherThanNull()
    {
        // A ChainedConfigurationProvider reports IsNullOrEmpty as "no value", so merely enabling secret
        // resolution used to turn every deliberately blank setting into null - a NullReferenceException
        // for a bound non-nullable string, and a silent fallback for every ?? "default".
        var innerRoot = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Feature:Suffix"] = string.Empty,
                ["Feature:Name"] = "x",
                ["Db:Password"] = "secret://app/db/pw"
            })
            .Build();

        var config = innerRoot.ResolveSecrets(
            hostServices: null,
            o => o.Namespace = "app",
            providers => providers.AddProvider<FakeReaderProvider>());

        Assert.Equal(string.Empty, config["Feature:Suffix"]);
        Assert.Equal(string.Empty, config.GetSection("Feature").GetChildren().Single(c => c.Key == "Suffix").Value);
        Assert.Equal("x", config["Feature:Name"]);
        Assert.Equal("resolved-pw", config["Db:Password"]);
    }

    [Fact]
    public void ChainedRoot_ReadsTheSameValuesAsTheUndecoratedRoot()
    {
        var values = new Dictionary<string, string?>
        {
            ["Empty"] = string.Empty,
            ["Set"] = "value",
            ["Nested:Empty"] = string.Empty,
            ["Nested:Set"] = "nested"
        };
        var innerRoot = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var undecorated = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var config = innerRoot.ResolveSecrets(
            hostServices: null,
            o => o.Namespace = "app",
            providers => providers.AddProvider<FakeReaderProvider>());

        foreach (var key in values.Keys)
        {
            Assert.Equal(undecorated[key], config[key]);
        }

        Assert.Null(config["NeverConfigured"]);
    }

    [Fact]
    public void ChainedRoot_HonoursTheInnerProviderOrder_ForOverriddenKeys()
    {
        // The chained provider reads the inner providers directly, so it has to reproduce the inner root's
        // own "last provider that has the key wins" precedence itself.
        var innerRoot = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Key"] = "first", ["Blanked"] = "value" })
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Key"] = "second", ["Blanked"] = string.Empty })
            .Build();

        var config = innerRoot.ResolveSecrets(
            hostServices: null,
            o => o.Namespace = "app",
            providers => providers.AddProvider<FakeReaderProvider>());

        Assert.Equal("second", config["Key"]);
        Assert.Equal(string.Empty, config["Blanked"]);
    }
    [Fact]
    public void ChainedRoot_ResolvesAReferenceThatOnlyAppearsOnReload()
    {
        var reloadableSource = new ReloadableSource(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Db:Host"] = "localhost"
        });
        var innerRoot = new ConfigurationBuilder().Add(reloadableSource).Build();
        var config = innerRoot.ResolveSecrets(
            hostServices: null,
            o => o.Namespace = "app",
            providers => providers.AddProvider<FakeReaderProvider>());

        var seenOnNotification = new List<string?>();
        ChangeToken.OnChange(config.GetReloadToken, () => seenOnNotification.Add(config["Db:Password"]));

        reloadableSource.Provider!.SetValues(new Dictionary<string, string?>
        {
            ["Db:Password"] = "secret://app/db/pw"
        });

        // The consumer must never observe the literal token: the inner root's own reload token is muted so
        // that this single notification is raised by the resolver, after it has re-resolved.
        Assert.Equal(["resolved-pw"], seenOnNotification);
        Assert.Equal("resolved-pw", config["Db:Password"]);
    }

    [Fact]
    public void ChainedRoot_ReportsAPlainValueChange_EvenWhenNoSecretChanged()
    {
        var reloadableSource = new ReloadableSource(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Db:Password"] = "secret://app/db/pw",
            ["Db:Host"] = "localhost"
        });
        var innerRoot = new ConfigurationBuilder().Add(reloadableSource).Build();
        var config = innerRoot.ResolveSecrets(
            hostServices: null,
            o => o.Namespace = "app",
            providers => providers.AddProvider<FakeReaderProvider>());

        var notifications = 0;
        ChangeToken.OnChange(config.GetReloadToken, () => notifications++);

        reloadableSource.Provider!.SetValues(new Dictionary<string, string?>
        {
            ["Db:Password"] = "secret://app/db/pw",
            ["Db:Host"] = "elsewhere"
        });

        Assert.Equal(1, notifications);
        Assert.Equal("elsewhere", config["Db:Host"]);
    }

    [Fact]
    public void ChainedRoot_Dispose_DisposesTheInnerRootExactlyOnce()
    {
        var innerSource = new DisposalTrackingSource();
        var innerRoot = new ConfigurationBuilder().Add(innerSource).Build();

        var config = innerRoot.ResolveSecrets(
            hostServices: null,
            o =>
            {
                o.Namespace = "app";
                o.ThrowOnUnresolvedReference = false;
            },
            providers => providers.AddProvider<FakeReaderProvider>());

        ((IDisposable)config).Dispose();

        Assert.Equal(1, innerSource.Provider!.DisposeCount);
    }

    [Fact]
    public void ResolveSecrets_Throws_OnNullRoot()
        => Assert.Throws<ArgumentNullException>(
            () => SecretConfigurationRootExtensions.ResolveSecrets(null!, hostServices: null));

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
    private sealed class ReloadableSource(Dictionary<string, string?>? initialValues = null) : IConfigurationSource
    {
        public ReloadableProvider? Provider { get; private set; }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            Provider = new ReloadableProvider(initialValues);
            return Provider;
        }
    }

    private sealed class ReloadableProvider(Dictionary<string, string?>? initialValues) : ConfigurationProvider
    {
        private Dictionary<string, string?> _values = initialValues
            ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["Db:Password"] = "secret://app/db/pw" };

        public override void Load() => Data = new Dictionary<string, string?>(_values, StringComparer.OrdinalIgnoreCase);

        public void TriggerReload() => OnReload();

        public void SetValues(Dictionary<string, string?> values)
        {
            _values = values;
            Load();
            OnReload();
        }
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

    // Counts how often the same secret is fetched, so a reference used by several configuration keys can
    // be shown to cost one store round-trip and not one per key.
    private sealed class CountingProvider : ISecretStoreProvider
    {
        public int CallCount { get; private set; }

        public string Name => "counting";

        public bool IsAvailable => true;

        public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<string?>(name == "app/db/pw" ? "resolved-pw" : null);
        }

        public Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveSecretAsync(string name, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    // Ignores the cancellation token entirely, the way a provider blocked in a synchronous native call does.
    private sealed class BlockingProvider : ISecretStoreProvider
    {
        public static readonly TimeSpan BlockFor = TimeSpan.FromSeconds(10);

        public string Name => "blocking";

        public bool IsAvailable => true;

        public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
        {
            Thread.Sleep(BlockFor);
            return Task.FromResult<string?>(null);
        }

        public Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveSecretAsync(string name, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NeverAnsweringProvider : ISecretStoreProvider
    {
        public string Name => "never-answering";

        public bool IsAvailable => true;

        public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return null;
        }

        public Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveSecretAsync(string name, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    // Records the builder properties it was handed, once per Build call, so a test can tell which builder
    // built it first.
    private sealed class PropertyCapturingSource : IConfigurationSource
    {
        public List<object?> CapturedProperties { get; } = [];

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            builder.Properties.TryGetValue("test-marker", out var marker);
            CapturedProperties.Add(marker);
            return new EmptyProvider();
        }
    }

    private sealed class CountingSource(Dictionary<string, string?> values) : IConfigurationSource
    {
        public int BuildCount { get; private set; }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            BuildCount++;
            return new StaticProvider(values);
        }
    }

    private sealed class DisposalTrackingSource : IConfigurationSource
    {
        public DisposalTrackingProvider? Provider { get; private set; }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            Provider = new DisposalTrackingProvider();
            return Provider;
        }
    }

    private sealed class DisposalTrackingProvider : ConfigurationProvider, IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }

    private sealed class EmptyProvider : ConfigurationProvider;

    private sealed class StaticProvider(Dictionary<string, string?> values) : ConfigurationProvider
    {
        public override void Load() => Data = new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase);
    }
}
