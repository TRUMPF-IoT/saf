// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.Tests.WireFormat;

using NATS.Client.Core;
using NSubstitute;
using SAF.Messaging.Nats.WireFormat;
using Xunit;

public class NatsWireMessageTests
{
    private readonly INatsClient _client = Substitute.For<INatsClient>();
    private readonly NatsHeaders _headers = new() { { "h", "v" } };

    /// <summary>
    /// NATS.Net encodes a string as UTF-8 itself, as in every earlier SAF version.
    /// </summary>
    [Theory]
    [InlineData("text")]
    [InlineData(null)]
    public void PublishesATextBodyAsString(string? body)
    {
        var wire = NatsWireMessage.Text(body, _headers);

        _ = wire.PublishAsync(_client, "a.b");

        var arguments = AssertPublished(typeof(string));
        Assert.Equal(body, arguments[1]);
        Assert.Null(wire.BinaryBody);
    }

    [Fact]
    public void PublishesABinaryBodyAsBytesWithoutCopying()
    {
        byte[] body = [1, 2];
        var wire = NatsWireMessage.Binary(body, _headers);

        _ = wire.PublishAsync(_client, "a.b");

        var arguments = AssertPublished(typeof(byte[]));
        Assert.Same(body, arguments[1]);
        Assert.Null(wire.TextBody);
    }

    private object?[] AssertPublished(Type bodyType)
    {
        var call = _client.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "PublishAsync");
        Assert.Equal(bodyType, call.GetMethodInfo().GetGenericArguments()[0]);
        var arguments = call.GetArguments();
        Assert.Equal("a.b", arguments[0]);
        Assert.Same(_headers, arguments[2]);
        return arguments;
    }
}
