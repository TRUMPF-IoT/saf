// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

internal interface ITsmMessageEncoder
{
    /// <summary>
    /// The encoder for a peer of the given pub/sub version. It picks the format once.
    /// </summary>
    /// <exception cref="NotSupportedException">No format exists for the version.</exception>
    ITsmPeerEncoder ForPeer(string peerVersion);
}
