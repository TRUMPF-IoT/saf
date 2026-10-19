// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.Tests;

using SAF.Messaging.Contracts;
using Xunit;

public class DroppedMessageWarningTests
{
    [Fact]
    public void Report_WarnsOnlyForTheFirstDroppedMessage()
    {
        var warnings = new List<string>();
        var warning = new DroppedMessageWarning(warnings.Add);

        warning.Report("peer", "4.0.0", new Message { Topic = "file/chunk", BinaryPayload = [1] });
        warning.Report("peer", "4.0.0", new Message { Topic = "file/chunk", BinaryPayload = [2] });
        warning.Report("peer", "4.0.0", new Message { Topic = "other", BinaryPayload = [3] });

        var single = Assert.Single(warnings);
        Assert.Contains("file/chunk", single);
        Assert.Contains("peer", single);
        Assert.Contains("4.0.0", single);
        Assert.Contains(nameof(MessageFormats.Binary), single);
    }

    [Fact]
    public void Report_WarnsOnce_WhenMessagesAreDroppedConcurrently()
    {
        var warnings = 0;
        var warning = new DroppedMessageWarning(_ => Interlocked.Increment(ref warnings));

        Parallel.For(0, 1000, i => warning.Report("peer", "4.0.0", new Message { Topic = $"t/{i}", BinaryPayload = [] }));

        Assert.Equal(1, warnings);
    }
}
