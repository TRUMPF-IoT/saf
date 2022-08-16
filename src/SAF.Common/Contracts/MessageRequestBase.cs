// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Common.Contracts;

/// <summary>
/// The base class for all messages treated as "request".
/// </summary>
public abstract class MessageRequestBase
{
    /// <summary>
    /// Gets or sets the channel name for the reply message.
    /// </summary>
    public string ReplyTo { get; set; }
}