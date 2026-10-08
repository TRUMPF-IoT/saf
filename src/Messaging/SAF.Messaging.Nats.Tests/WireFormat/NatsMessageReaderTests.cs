// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.Tests.WireFormat;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using NSubstitute;
using SAF.Messaging.Contracts;
using SAF.Messaging.Nats.WireFormat;
using TestUtilities;
using Xunit;
using static TestWireFormat;

public class NatsMessageReaderTests
{
    [Fact]
    public void Read_ReadsHeaderlessMessagesAsBefore()
    {
        var message = TestWireFormat.Reader().Read("t", Utf8("p"), null);

        Assert.Equal("t", message!.Topic);
        Assert.Equal("p", message.Payload);
        Assert.Null(message.CustomProperties);
    }

    [Fact]
    public void Read_IgnoresForeignHeadersWithoutVersion()
    {
        var message = TestWireFormat.Reader().Read("t", Utf8("p"), new NatsHeaders { { "trace-id", "1" } });

        Assert.Equal("p", message!.Payload);
    }

    [Theory]
    [InlineData("2.0.0")]
    [InlineData("2.4.0")]
    public void Read_ReadsMetadataOfKnownMajorVersions(string version)
    {
        var headers = new NatsHeaders
        {
            { NatsHeaderNames.Version, version },
            { NatsHeaderNames.Metadata, """{"customProperties":[{"name":"n","value":"v"}]}""" }
        };

        var message = TestWireFormat.Reader().Read("t", Utf8("p"), headers);

        Assert.Equal("v", Assert.Single(message!.CustomProperties!).Value);
    }

    [Theory]
    [InlineData("4.0.0")]
    [InlineData("0.1.0")]
    [InlineData("abc")]
    public void Read_DropsUnknownVersionsAndWarns(string version)
    {
        var logger = Substitute.For<MockLogger>();

        var message = TestWireFormat.Reader(logger).Read("t", Utf8("p"), new NatsHeaders { { NatsHeaderNames.Version, version } });

        Assert.Null(message);
        logger.Received(1).Log(LogLevel.Warning, Arg.Is<string>(m => m.Contains(version)));
    }

    [Fact]
    public void Read_DropsUnreadableMetadataAndWarns()
    {
        var logger = Substitute.For<MockLogger>();
        var headers = new NatsHeaders { { NatsHeaderNames.Version, "2.0.0" }, { NatsHeaderNames.Metadata, "{" } };

        var message = TestWireFormat.Reader(logger).Read("t", Utf8("p"), headers);

        Assert.Null(message);
        logger.Received(1).Log(LogLevel.Warning, Arg.Is<string>(m => m.Contains("cannot be read")));
    }

    /// <summary>
    /// A new version is added by registering a format, without touching the reader.
    /// </summary>
    [Fact]
    public void Read_DispatchesToTheFormatRegisteredForTheMajorVersion()
    {
        var expected = new Message { Topic = "t" };
        var v4 = Substitute.For<INatsWireFormat>();
        v4.MajorVersion.Returns(4);
        v4.Read("t", Arg.Any<NatsBody>(), Arg.Any<NatsHeaders?>()).Returns(expected);
        var reader = new NatsMessageReader([..NatsWireFormats.All, v4], new UnknownVersionWarning(NullLogger.Instance), NullLogger.Instance);

        var message = reader.Read("t", Utf8("p"), new NatsHeaders { { NatsHeaderNames.Version, "4.1.0" } });

        Assert.Same(expected, message);
    }

    [Fact]
    public void Read_ReadsBinaryPayloadsOfVersion3()
    {
        var message = TestWireFormat.Reader().Read("t", new NatsBody(new byte[] { 0, 255 }), new NatsHeaders { { NatsHeaderNames.Version, "3.0.0" } });

        Assert.Equal(new byte[] { 0, 255 }, message!.BinaryPayload);
    }
}
