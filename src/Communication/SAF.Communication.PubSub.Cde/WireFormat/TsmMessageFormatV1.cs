// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

using SAF.Messaging.Contracts;
using Interfaces;

/// <summary>
/// The payload alone; the topic travels in the TSM text.
/// </summary>
internal sealed class TsmMessageFormatV1 : ITsmMessageFormat
{
    public Version MinimumVersion { get; } = Version.Parse(PubSubVersion.V1);

    public string? Encode(Message message) => message.Payload;

    public Message Decode(string channel, string? pls) => new() { Topic = channel, Payload = pls };
}
