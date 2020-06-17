// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿namespace SAF.Common
{
    /// <summary>
    ///     A pub / sub message.
    /// </summary>
    public class Message
    {
        /// <summary>
        ///     Gets or sets the topic where the message is published.
        /// </summary>
        public string Topic { get; set; }

        /// <summary>
        ///     Gets or sets the payload of the message (usually JSON).
        /// </summary>
        public string Payload { get; set; }
    }
}