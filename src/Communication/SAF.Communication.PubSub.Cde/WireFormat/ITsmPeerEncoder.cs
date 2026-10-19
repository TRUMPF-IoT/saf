// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

using SAF.Messaging.Contracts;

/// <summary>
/// Encodes messages for one peer, in the format of its pub/sub version.
/// </summary>
internal interface ITsmPeerEncoder
{
    /// <summary>
    /// Whether the peer can receive the message. Messages it cannot are not encoded.
    /// </summary>
    bool CanEncode(Message message);

    /// <summary>
    /// The TSM payload for one message.
    /// </summary>
    TsmPayload Encode(Message message);

    /// <summary>
    /// The TSM payload for several messages at once.
    /// </summary>
    /// <exception cref="NotSupportedException">The peer's version does not support batches.</exception>
    TsmPayload EncodeBatch(IEnumerable<Message> messages);
}
