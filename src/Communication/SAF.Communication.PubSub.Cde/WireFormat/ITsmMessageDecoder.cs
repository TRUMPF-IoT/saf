// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

using SAF.Messaging.Contracts;

internal interface ITsmMessageDecoder
{
    bool IsBatch(string channel, string version);

    Message? Decode(string channel, string version, string? pls);

    List<Message>? DecodeBatch(string version, string? pls);

    /// <summary>
    /// Decodes a publish TSM regardless of whether it carries a single message or a batch.
    /// </summary>
    List<Message> DecodeMessages(Topic topic, string? pls);
}
