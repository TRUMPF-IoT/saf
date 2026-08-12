// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Authenticode;

using System.Runtime.InteropServices;

internal static class SnapshotStream
{
    /// <summary>
    /// Exposes an assembly content snapshot as a stream.
    /// </summary>
    /// <remarks>
    /// Reading the PE headers and computing the Authenticode hash both need a <see cref="Stream"/>.
    /// Snapshots arrive array-backed on every route through this library, so the stream reads that array
    /// in place; only memory that exposes no array is copied, which multi-MB plugin images would otherwise
    /// put on the large object heap once per check.
    /// </remarks>
    public static Stream Create(ReadOnlyMemory<byte> snapshot)
        => MemoryMarshal.TryGetArray(snapshot, out var segment) && segment.Array is not null
            ? new MemoryStream(segment.Array, segment.Offset, segment.Count, writable: false)
            : new MemoryStream(snapshot.ToArray(), writable: false);
}
