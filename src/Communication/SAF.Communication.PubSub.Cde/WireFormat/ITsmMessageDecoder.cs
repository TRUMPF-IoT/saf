// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

using SAF.Messaging.Contracts;

internal interface ITsmMessageDecoder
{
    bool IsBatch(string channel, string version);

    Message? Decode(string channel, string version, TsmPayload payload);

    List<Message>? DecodeBatch(string version, TsmPayload payload);

    /// <summary>
    /// Decodes a publish TSM regardless of whether it carries a single message or a batch.
    /// Returns <c>null</c> if the payload cannot be read.
    /// </summary>
    List<Message>? DecodeMessages(Topic topic, TsmPayload payload);
}
