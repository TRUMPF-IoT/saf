// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

using SAF.Messaging.Contracts;

/// <summary>
/// A message shape that can also be sent as a batch of several messages in one TSM.
/// </summary>
internal interface ITsmBatchFormat : ITsmMessageFormat
{
    TsmPayload EncodeBatch(IEnumerable<Message> messages);

    /// <summary>
    /// Reads all messages of a batch, or returns <c>null</c> if the payload is not of this shape.
    /// </summary>
    List<Message>? DecodeBatch(TsmPayload payload);
}
