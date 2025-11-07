// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Collections.Concurrent;
using SAF.Communication.PubSub.Cde.MessageProcessing;
using SAF.Communication.PubSub.Interfaces;
using SAF.Common;
using Xunit;

namespace SAF.Communication.PubSub.Cde.Tests.MessageProcessing;

public class BroadcastMessageQueueTests
{
    [Fact]
    public void Enqueue_SingleMessage_ProcessesImmediately()
    {
        var processed = new ConcurrentBag<BroadcastMessage>();

        using var doneEvent = new ManualResetEventSlim();
        var queue = new BroadcastMessageQueue((_, messages) =>
        {
            foreach (var m in messages) processed.Add(m);
            doneEvent.Set();
        });

        var msg = CreateBroadcastMessage("user1");
        queue.Enqueue(msg);

        Assert.True(doneEvent.Wait(TimeSpan.FromSeconds(5)), "Processing did not finish in time");
        Assert.Single(processed);
        Assert.Contains(processed, m => m == msg);
    }

    [Fact]
    public void Enqueue_MultipleMessagesSameUser_ProcessedInSingleBatch()
    {
        var calls = 0; var count = 0;

        using var doneEvent = new ManualResetEventSlim();
        var queue = new BroadcastMessageQueue((user, messages) =>
        {
            calls++;
            count += messages.Count();

            doneEvent.Set();
        });

        queue.Enqueue(CreateBroadcastMessage("user1"));
        queue.Enqueue(CreateBroadcastMessage("user1"));
        queue.Enqueue(CreateBroadcastMessage("user1"));

        Assert.True(doneEvent.Wait(TimeSpan.FromSeconds(5)), "Processing did not finish in time");
        Assert.Equal(1, calls);
        Assert.Equal(3, count);
    }

    [Fact]
    public void Enqueue_MessagesDifferentUsers_SeparateBatches()
    {
        var calls = 0;
        using var doneEvent = new ManualResetEventSlim();
        var queue = new BroadcastMessageQueue((_, messages) =>
        {
            calls++;
            if (calls == 2)
            {
                doneEvent.Set();
            }
        });

        queue.Enqueue(CreateBroadcastMessage("userA"));
        queue.Enqueue(CreateBroadcastMessage("userB"));

        Assert.True(doneEvent.Wait(TimeSpan.FromSeconds(5)), "Did not process both user batches");
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Enqueue_MessageAddedDuringProcessing_IsProcessedInNextIteration()
    {
        var calls = 0; 
        
        using var doneEvent = new ManualResetEventSlim();

        BroadcastMessageQueue? queue = null;
        queue = new BroadcastMessageQueue((_, messages) =>
        {
            calls++;
            if (calls == 1)
            {
                queue!.Enqueue(CreateBroadcastMessage("user1"));
            }

            if (calls == 2)
            {
                doneEvent.Set();
            }
        });

        queue.Enqueue(CreateBroadcastMessage("user1"));

        Assert.True(doneEvent.Wait(TimeSpan.FromSeconds(5)), "Second iteration not processed");
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Enqueue_ConcurrentAdds_StartsOnlyOneProcessingLoop()
    {
        HashSet<int?> taskIds = [];
        var totalProcessed = 0;
        
        using var doneEvent = new ManualResetEventSlim();
        var queue = new BroadcastMessageQueue((_, messages) =>
        {
            taskIds.Add(Task.CurrentId);

            Interlocked.Add(ref totalProcessed, messages.Count());
            if (totalProcessed >= 50)
            {
                doneEvent.Set();
            }
        });

        Parallel.For(0, 50, _ => queue.Enqueue(CreateBroadcastMessage("userX")));

        Assert.True(doneEvent.Wait(TimeSpan.FromSeconds(5)), "Not all messages processed in time");
        Assert.Equal(50, totalProcessed);
        Assert.Equal(1, taskIds.Count);
    }

    private static BroadcastMessage CreateBroadcastMessage(string userId, string channel = "chan", string payload = "data")
    {
        var topic = new Topic(channel, Guid.NewGuid().ToString("N"), PubSubVersion.V1);
        var message = new Message { Topic = channel, Payload = payload };
        return new BroadcastMessage(topic, message, userId, RoutingOptions.All);
    }
}
