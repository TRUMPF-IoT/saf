// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Contracts;

/// <summary>
/// The base class for JSON request payloads that carry their reply topic themselves.
/// </summary>
/// <remarks>
/// Kept for compatibility: SAF 10.x and older read and write the reply topic only here.
/// From SAF 11.x on, <see cref="Message.ReplyTo"/> carries it as well.
/// </remarks>
public abstract class MessageRequestBase
{
    /// <summary>
    /// Gets or sets the channel name for the reply message.
    /// </summary>
    public string? ReplyTo { get; set; }
}
