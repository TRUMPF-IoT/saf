// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Toolbox.FileTransfer;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SAF.Toolbox.Tests.FileTransfer;

public class FileTransferLockManagerTests
{
    [Fact]
    public void Acquire_ReturnsDisposable()
    {
        var manager = new FileTransferLockManager();
        const string fileId = "file1";

        using var lock1 = manager.Acquire(fileId);

        Assert.NotNull(lock1);
    }

    [Fact]
    public void Acquire_BlocksOtherThreads_UntilReleased()
    {
        var manager = new FileTransferLockManager();
        const string fileId = "file2";

        var lock1 = manager.Acquire(fileId);

        var acquired = false;
        var t = new Thread(() =>
        {
            using var lock2 = manager.Acquire(fileId);
            acquired = true;
        });

        t.Start();
        Thread.Sleep(250); // Give thread time to block on Acquire
        Assert.False(acquired);

        lock1.Dispose();

        t.Join(1000);
        Assert.True(acquired);
    }

    [Fact]
    public void Acquire_ReleasesAndRemovesLock_WhenLastDisposed()
    {
        var manager = new FileTransferLockManager();
        const string fileId = "file3";

        var lock1 = manager.Acquire(fileId);
        lock1.Dispose();

        using var lock3 = manager.Acquire(fileId);
        Assert.NotNull(lock3);
    }

    [Fact]
    public void Acquire_DifferentFileIds_AreIndependent()
    {
        var manager = new FileTransferLockManager();
        using var lock1 = manager.Acquire("fileA");
        using var lock2 = manager.Acquire("fileB");
        Assert.NotNull(lock1);
        Assert.NotNull(lock2);
    }
}