// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿namespace SAF.Common
{
    /// <summary>
    ///     Represents a handler for a system message.
    /// </summary>
    public interface IMessageHandler
    {
        /// <summary>
        ///     Determines whether this handler can handle the specified message.
        /// </summary>
        /// <param name="message">The message to check.</param>
        /// <returns>
        ///     <c>true</c> if this instance can handle the specified message; otherwise, <c>false</c>.
        /// </returns>
        bool CanHandle(Message message);

        /// <summary>
        ///     Handles the specified message.
        /// </summary>
        /// <param name="message">The message to handle.</param>
        void Handle(Message message);
    }
}