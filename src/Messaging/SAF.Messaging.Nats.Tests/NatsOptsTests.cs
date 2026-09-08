// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using System.Threading.Channels;
using NATS.Client.Core;
using Xunit;

namespace SAF.Messaging.Nats.Tests;

public class NatsOptsTests
{
    [Fact]
    public void CreateNatsOpts_ShouldWaitOnFullSubscriptionChannel_InsteadOfDroppingMessages()
    {
        var opts = ServiceCollectionExtensions.CreateNatsOpts(new NatsConfiguration());

        // NatsOpts defaults to DropNewest and NATS.Net 3.x no longer overrides it in the NatsClient ctor.
        Assert.Equal(BoundedChannelFullMode.Wait, opts.SubPendingChannelFullMode);
    }

    [Fact]
    public void CreateNatsOpts_ShouldMapConfiguration()
    {
        var config = new NatsConfiguration
        {
            Url = "nats://localhost:4222",
            Verbose = true,
            CommandTimeout = TimeSpan.FromSeconds(11),
            RequestTimeout = TimeSpan.FromSeconds(12),
            MaxReconnectRetry = 7,
            AuthOpts = new NatsConfigurationAuthOpts { Username = "user", Password = "pw" },
            TlsOpts = new NatsConfigurationTlsOpts { CaFile = "ca.pem", InsecureSkipVerify = true }
        };

        var opts = ServiceCollectionExtensions.CreateNatsOpts(config);

        Assert.Equal(config.Url, opts.Url);
        Assert.True(opts.Verbose);
        Assert.Equal(config.CommandTimeout, opts.CommandTimeout);
        Assert.Equal(config.RequestTimeout, opts.RequestTimeout);
        Assert.Equal(config.MaxReconnectRetry, opts.MaxReconnectRetry);
        Assert.Equal("user", opts.AuthOpts.Username);
        Assert.Equal("pw", opts.AuthOpts.Password);
        Assert.Equal("ca.pem", opts.TlsOpts.CaFile);
        Assert.True(opts.TlsOpts.InsecureSkipVerify);
    }
}
