// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.FileTransfer;

using System.Collections.Concurrent;

internal sealed class FileTransferLockManager
{
    private sealed class SyncEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int LockCount { get; set; }
    }

    private readonly ConcurrentDictionary<string, SyncEntry> _locks = new();

    public IDisposable Acquire(string fileId)
    {
        SyncEntry? entry;
        lock (_locks)
        {
            if (!_locks.TryGetValue(fileId, out entry))
            {
                entry = new SyncEntry();
                _locks[fileId] = entry;
            }

            entry.LockCount++;
        }

        entry.Semaphore.Wait();

        return new Releaser(() =>
        {
            entry.Semaphore.Release();

            lock (_locks)
            {
                if (--entry.LockCount != 0) return;
                _locks.TryRemove(fileId, out _);
                entry.Semaphore.Dispose();
            }
        });
    }

    private sealed class Releaser(Action release) : IDisposable
    {
        public void Dispose() => release();
    }
}