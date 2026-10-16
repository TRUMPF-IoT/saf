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

    public bool CanEncode(Message message) => message.BinaryPayload is null;

    public TsmPayload Encode(Message message) => new(message.Payload);

    public Message Decode(string channel, TsmPayload payload) => new() { Topic = channel, Payload = payload.Pls };
}
