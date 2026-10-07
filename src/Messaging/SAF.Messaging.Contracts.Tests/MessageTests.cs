// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Contracts.Tests;

using Xunit;

public class MessageTests
{
    [Fact]
    public void NewMessage_CarriesNoPayloadAndNoMetadata()
    {
        var message = new Message { Topic = "t" };

        Assert.Null(message.Payload);
        Assert.Null(message.BinaryPayload);
        Assert.Null(message.AcceptedReplyFormats);
        Assert.Null(message.CustomProperties);
    }

    [Theory]
    [InlineData(null, false, MessageFormats.None)]
    [InlineData("p", false, MessageFormats.Text)]
    [InlineData("", false, MessageFormats.Text)]
    [InlineData(null, true, MessageFormats.Binary)]
    [InlineData("p", true, MessageFormats.Text | MessageFormats.Binary)]
    public void GetFormat_ReportsThePayloadsPresent(string? payload, bool hasBinaryPayload, MessageFormats expected)
    {
        var message = new Message { Topic = "t", Payload = payload, BinaryPayload = hasBinaryPayload ? [] : null };

        Assert.Equal(expected, message.GetFormat());
    }

    /// <summary>
    /// Transports send the values as numbers, so they are part of the wire format.
    /// </summary>
    [Theory]
    [InlineData(MessageFormats.None, 0)]
    [InlineData(MessageFormats.Text, 1)]
    [InlineData(MessageFormats.Binary, 2)]
    public void MessageFormats_KeepTheirWireValues(MessageFormats format, int expected)
        => Assert.Equal(expected, (int)format);
}
