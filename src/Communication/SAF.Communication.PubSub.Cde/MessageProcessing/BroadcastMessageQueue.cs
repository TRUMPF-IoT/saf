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
    private bool _queueProcessing;

    public void Enqueue(BroadcastMessage message)
    {
        lock (_syncQueue)
        {
            _queue.Add(message);

            if (_queueProcessing) return;
            _queueProcessing = true;

            ProcessQueuedMessagesAsync().ConfigureAwait(false);
        }
    }

    private Task ProcessQueuedMessagesAsync()
        => Task.Run(() =>
        {
            var messagesToProcess = GetMessagesToBroadcast();
            while (messagesToProcess.Count > 0)
            {
                var messagesToSendPerUser = messagesToProcess.GroupBy(p => p.UserId);
                foreach (var broadcastMessages in messagesToSendPerUser)
                {
                    messageProcessing(broadcastMessages.Key, broadcastMessages);
                }

                messagesToProcess = GetMessagesToBroadcast();
            }
        });

    private IReadOnlyList<BroadcastMessage> GetMessagesToBroadcast()
    {
        lock (_syncQueue)
        {
            if (_queue.Count == 0)
            {
                _queueProcessing = false;
                return [];
            }

            var messages = _queue;
            _queue = [];
            return messages;
        }
    }
}