// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

using SAF.Messaging.Contracts;

/// <summary>
/// The shape of the inner format for pub/sub versions that cannot carry binary payloads.
/// </summary>
internal sealed class TsmTextOnlyFormat(ITsmBatchFormat format, Version minimumVersion) : ITsmBatchFormat
{
    public Version MinimumVersion => minimumVersion;

    public bool CanEncode(Message message) => message.BinaryPayload is null && format.CanEncode(message);

    public TsmPayload Encode(Message message) => format.Encode(message);

    // These versions carry no PLB; as in 10.x, a PLB does not make the message unreadable.
    public Message? Decode(string channel, TsmPayload payload) => format.Decode(channel, payload with { Plb = null });

    public TsmPayload EncodeBatch(IEnumerable<Message> messages) => format.EncodeBatch(messages);

    public List<Message>? DecodeBatch(TsmPayload payload) => format.DecodeBatch(payload with { Plb = null });
}
