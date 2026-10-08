// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

using SAF.Messaging.Contracts;

/// <summary>
/// One message shape in the payload of a publish TSM. A new shape is added as a new implementation.
/// </summary>
internal interface ITsmMessageFormat
{
    /// <summary>
    /// The first pub/sub version using this shape. It serves every later version until a newer shape takes over.
    /// </summary>
    Version MinimumVersion { get; }

    bool CanEncode(Message message);

    TsmPayload Encode(Message message);

    /// <summary>
    /// Reads one message, or returns <c>null</c> if the payload is not of this shape.
    /// </summary>
    Message? Decode(string channel, TsmPayload payload);
}
