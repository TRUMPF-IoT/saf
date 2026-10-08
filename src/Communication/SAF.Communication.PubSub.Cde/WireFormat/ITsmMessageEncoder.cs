// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

using SAF.Messaging.Contracts;

internal interface ITsmMessageEncoder
{
    /// <summary>
    /// Whether a peer of the given pub/sub version can receive the message. Messages it cannot are not encoded.
    /// </summary>
    bool CanEncode(Message message, string peerVersion);

    /// <summary>
    /// The TSM payload for one message to a peer of the given pub/sub version.
    /// </summary>
    TsmPayload Encode(Message message, string peerVersion);

    /// <summary>
    /// The TSM payload for several messages at once to a peer of the given pub/sub version.
    /// </summary>
    TsmPayload EncodeBatch(IEnumerable<Message> messages, string peerVersion);
}
