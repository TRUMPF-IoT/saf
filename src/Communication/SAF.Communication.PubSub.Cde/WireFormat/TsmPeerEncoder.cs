// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

using SAF.Messaging.Contracts;

internal sealed class TsmPeerEncoder(ITsmMessageFormat format, string peerVersion) : ITsmPeerEncoder
{
    public bool CanEncode(Message message) => format.CanEncode(message);

    public TsmPayload Encode(Message message)
    {
        EnsureCanEncode(message);
        return format.Encode(message);
    }

    public TsmPayload EncodeBatch(IEnumerable<Message> messages)
    {
        var batchFormat = format as ITsmBatchFormat
                          ?? throw new NotSupportedException($"Pub/sub version {peerVersion} does not support batches.");
        var list = messages.ToList();
        foreach (var message in list) EnsureCanEncode(message);
        return batchFormat.EncodeBatch(list);
    }

    private void EnsureCanEncode(Message message)
    {
        if (!format.CanEncode(message))
            throw new InvalidOperationException($"Pub/sub version {peerVersion} cannot carry the message on {message.Topic}.");
    }
}
