// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.Tests;

using Microsoft.Extensions.Configuration;
using SAF.Messaging.Contracts;
using Xunit;

public class EnableBinaryPayloadsTests
{
    [Fact]
    public void IsOnByDefault()
        => Assert.True(new NatsConfiguration().EnableBinaryPayloads);

    [Fact]
    public void BindsFromTheNatsSection()
    {
        var section = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Nats:EnableBinaryPayloads"] = "false" })
            .Build()
            .GetSection("Nats");
        var config = new NatsConfiguration();

        section.Bind(config);

        Assert.False(config.EnableBinaryPayloads);
    }

    [Fact]
    public void ARouteWithoutOwnValuesUsesTheNatsSection()
    {
        var defaults = new NatsConfiguration { Url = "nats://host", EnableBinaryPayloads = false };

        Assert.Same(defaults, ServiceCollectionExtensions.ResolveConfiguration(new MessagingConfiguration(), defaults));
    }

    /// <summary>
    /// A route's own values replace the Nats section, but the switch is inherited: one setting covers the deployment.
    /// </summary>
    [Fact]
    public void ARouteInheritsTheSwitchFromTheNatsSection()
    {
        var defaults = new NatsConfiguration { Url = "nats://host", EnableBinaryPayloads = false };
        var route = new MessagingConfiguration { Config = new Dictionary<string, string> { ["Url"] = "nats://route" } };

        var resolved = ServiceCollectionExtensions.ResolveConfiguration(route, defaults);

        Assert.Equal("nats://route", resolved.Url);
        Assert.False(resolved.EnableBinaryPayloads);
    }

    [Fact]
    public void ARouteOverridesTheSwitch()
    {
        var defaults = new NatsConfiguration { Url = "nats://host", EnableBinaryPayloads = true };
        var route = new MessagingConfiguration { Config = new Dictionary<string, string> { ["EnableBinaryPayloads"] = "false" } };

        Assert.False(ServiceCollectionExtensions.ResolveConfiguration(route, defaults).EnableBinaryPayloads);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheWriterSendsBinaryPayloadsOnlyWhenEnabled(bool enabled)
    {
        var writer = ServiceCollectionExtensions.CreateWriter(new NatsConfiguration { EnableBinaryPayloads = enabled });

        Assert.Equal(enabled, writer.TryWrite(new Message { Topic = "t", BinaryPayload = [1] }, out _, out _));
        Assert.True(writer.TryWrite(new Message { Topic = "t", Payload = "p" }, out _, out _));
    }
}
