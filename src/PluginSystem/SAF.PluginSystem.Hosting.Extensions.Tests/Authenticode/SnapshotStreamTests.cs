// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests.Authenticode;

using SAF.PluginSystem.Hosting.Extensions.Authenticode;
using System.Buffers;

public class SnapshotStreamTests
{
    [Fact]
    public void Create_ExposesTheSliceOfAnArrayBackedSnapshot()
    {
        var content = new byte[512];
        Random.Shared.NextBytes(content);
        var snapshot = content.AsMemory(64, 128);

        using var stream = SnapshotStream.Create(snapshot);
        var read = new byte[stream.Length];
        stream.ReadExactly(read);

        Assert.Equal(128, stream.Length);
        Assert.Equal(snapshot.ToArray(), read);
    }

    [Fact]
    public void Create_ReadsSnapshotsThatExposeNoArray()
    {
        var content = new byte[256];
        Random.Shared.NextBytes(content);
        using var manager = new ArraylessMemoryManager(content);

        using var stream = SnapshotStream.Create(manager.Memory);
        var read = new byte[stream.Length];
        stream.ReadExactly(read);

        Assert.Equal(content, read);
    }

    private sealed class ArraylessMemoryManager(byte[] content) : MemoryManager<byte>
    {
        public override Span<byte> GetSpan() => content;

        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();

        public override void Unpin() { }

        protected override void Dispose(bool disposing) { }
    }
}
