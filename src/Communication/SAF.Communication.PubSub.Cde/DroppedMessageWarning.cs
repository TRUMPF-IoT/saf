// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde;

using SAF.Messaging.Contracts;

/// <summary>
/// Warns once per peer instead of once per dropped message: a file sent in binary chunks would warn thousands of times.
/// </summary>
internal sealed class DroppedMessageWarning(Action<string> warn) : IDroppedMessageWarning
{
    private int _reported;

    public void Report(string peer, string peerVersion, Message message)
    {
        if (Interlocked.Exchange(ref _reported, 1) == 1) return;

        warn($"Dropped message on {message.Topic} for {peer}: pub/sub version {peerVersion} cannot transport a message with format {message.GetFormat()}. Further messages this peer cannot receive are dropped without a warning.");
    }
}
