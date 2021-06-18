// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Collections.Generic;

namespace SAF.Common
{
    /// <summary>
    /// A pub/sub message.
    /// </summary>
    public class Message
    {
        /// <summary>
        /// Gets or sets the topic where the message is published.
        /// </summary>
        public string Topic { get; set; }

        /// <summary>
        /// Gets or sets the payload of the message (usually JSON).
        /// </summary>
        public string Payload { get; set; }

        /// <summary>
        /// Gets or sets custom properties of the message.
        /// Custom properties are string key-value pairs used to add metadata to SAF messages.
        /// </summary>
        public List<MessageCustomProperty> CustomProperties { get; set; }
    }

    public class MessageCustomProperty
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}