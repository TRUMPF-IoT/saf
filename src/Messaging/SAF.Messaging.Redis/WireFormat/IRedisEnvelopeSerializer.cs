// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

using SAF.Messaging.Contracts;

/// <summary>
/// Writes and reads the JSON envelope that the envelope formats share.
/// </summary>
internal interface IRedisEnvelopeSerializer
{
    string Serialize(string version, Message message);

    /// <returns><c>null</c> if the envelope carries no readable message.</returns>
    Message? Deserialize(string envelope);
}
