// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

using SAF.Messaging.Contracts;

internal interface ITsmMessageEncoder
{
    /// <summary>
    /// The TSM payload for one message to a peer of the given pub/sub version.
    /// </summary>
    string? Encode(Message message, string peerVersion);

    /// <summary>
    /// The TSM payload for several messages at once to a peer of the given pub/sub version.
    /// </summary>
    string EncodeBatch(IEnumerable<Message> messages, string peerVersion);
}
