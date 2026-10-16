// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.WireFormat;

using StackExchange.Redis;

/// <summary>
/// A Redis value that carries a binary payload after the JSON envelope.
/// </summary>
internal interface IRedisBinaryFrame
{
    bool IsFrame(RedisValue value);

    RedisValue Write(string envelope, byte[] binaryPayload);

    /// <returns>The layout version of a frame from a newer node, which this node cannot read; otherwise <c>null</c>.</returns>
    byte? GetNewerLayoutVersion(RedisValue frame);

    /// <returns><c>null</c> if the frame cannot be read.</returns>
    RedisWireValue? Read(RedisValue frame);
}
