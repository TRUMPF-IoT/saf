// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Common;
using SAF.Communication.PubSub.Interfaces;

namespace SAF.Communication.PubSub.Cde.MessageProcessing;

public record BroadcastMessage(Topic Topic, Message Message, string UserId, RoutingOptions RoutingOptions);

public class BroadcastMessageQueue(Action<string, IEnumerable<BroadcastMessage>> messageProcessing)
{
    private readonly object _syncQueue = new();
    private List<BroadcastMessage> _queue = [];

    public void Enqueue(BroadcastMessage message)
    {
        lock (_syncQueue)
        {
            var triggerProcess = _queue.Count == 0;
            _queue.Add(message);

            if (triggerProcess)
            {
                ProcessQueuedMessagesAsync().ConfigureAwait(false);
            }
        }
    }

    private Task ProcessQueuedMessagesAsync()
        => Task.Run(() =>
        {
            List<BroadcastMessage> messagesToProcess;
            lock (_syncQueue)
            {
                messagesToProcess = _queue;
                _queue = [];
            }

            var messagesToSendPerUser = messagesToProcess.GroupBy(p => p.UserId);
            foreach (var broadcastMessages in messagesToSendPerUser)
            {
                messageProcessing(broadcastMessages.Key, broadcastMessages);
            }
        });
}