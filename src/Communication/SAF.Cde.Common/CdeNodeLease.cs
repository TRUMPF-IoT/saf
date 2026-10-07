// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Cde.Common;

/// <summary>
/// A plug-in container's hold on the C-DEngine node of the process.
/// </summary>
/// <remarks>
/// <see cref="ServiceCollectionExtensions.AddCde"/> registers one lease per container. Resolving it starts
/// C-DEngine if no other container has done so yet; disposing the last lease of the process shuts C-DEngine
/// down. Services that need a running node resolve the lease before they use C-DEngine, so the container
/// disposes them before the lease.
/// </remarks>
public sealed class CdeNodeLease : IDisposable
{
    private readonly Action _release;
    private int _disposed;

    internal CdeNodeLease(Action release) => _release = release;

    /// <summary>
    /// Releases the lease. The last lease of the process shuts C-DEngine down.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _release();
        }
    }
}