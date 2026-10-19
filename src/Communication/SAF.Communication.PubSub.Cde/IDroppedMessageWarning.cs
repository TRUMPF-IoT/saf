// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde;

using SAF.Messaging.Contracts;

/// <summary>
/// Reports messages that are not sent to a peer because its pub/sub version cannot carry them.
/// </summary>
internal interface IDroppedMessageWarning
{
    void Report(string peer, string peerVersion, Message message);
}
