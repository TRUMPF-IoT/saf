// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.Tests;

using Microsoft.Extensions.Configuration;
using SAF.Messaging.Contracts;
using Xunit;

public class EnableBinaryPayloadsTests
{
    [Fact]
    public void IsOnByDefault()
        => Assert.True(new RedisConfiguration().EnableBinaryPayloads);

    [Fact]
    public void BindsFromTheRedisSection()
    {
        var section = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Redis:EnableBinaryPayloads"] = "false" })
            .Build()
            .GetSection("Redis");
        var config = new RedisConfiguration();

        section.Bind(config);

        Assert.False(config.EnableBinaryPayloads);
    }

    [Fact]
    public void ARouteWithoutOwnValuesUsesTheRedisSection()
    {
        var defaults = new RedisConfiguration { ConnectionString = "host", EnableBinaryPayloads = false };

        Assert.Same(defaults, ServiceCollectionExtensions.ResolveConfiguration(new MessagingConfiguration(), defaults));
    }

    [Fact]
    public void ARouteInheritsTheSwitchFromTheRedisSection()
    {
        var defaults = new RedisConfiguration { ConnectionString = "host", Timeout = 5, EnableBinaryPayloads = false };
        var route = new MessagingConfiguration { Config = new Dictionary<string, string> { ["connectionString"] = "route" } };

        var resolved = ServiceCollectionExtensions.ResolveConfiguration(route, defaults);

        Assert.Equal("route", resolved.ConnectionString);
        Assert.Equal(5, resolved.Timeout);
        Assert.False(resolved.EnableBinaryPayloads);
    }

    [Fact]
    public void ARouteOverridesTheSwitch()
    {
        var defaults = new RedisConfiguration { ConnectionString = "host", EnableBinaryPayloads = true };
        var route = new MessagingConfiguration { Config = new Dictionary<string, string> { ["enableBinaryPayloads"] = "false" } };

        var resolved = ServiceCollectionExtensions.ResolveConfiguration(route, defaults);

        Assert.Equal("host", resolved.ConnectionString);
        Assert.False(resolved.EnableBinaryPayloads);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheWriterSendsBinaryPayloadsOnlyWhenEnabled(bool enabled)
    {
        var writer = ServiceCollectionExtensions.CreateWriter(new RedisConfiguration { EnableBinaryPayloads = enabled });

        Assert.Equal(enabled, writer.TryWrite(new Message { Topic = "t", BinaryPayload = [1] }, out _, out _));
        Assert.True(writer.TryWrite(new Message { Topic = "t", Payload = "p" }, out _, out _));
    }
}
