// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Contracts;

/// <summary>
/// Payload formats of a <see cref="Message"/>.
/// </summary>
/// <remarks>
/// Transports send these values as numbers, so existing values must never change.
/// </remarks>
[Flags]
public enum MessageFormats
{
    None = 0,

    /// <summary>
    /// <see cref="Message.Payload"/>.
    /// </summary>
    Text = 1,

    /// <summary>
    /// <see cref="Message.BinaryPayload"/>.
    /// </summary>
    Binary = 2
}
