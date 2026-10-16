// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

/// <summary>
/// The binary payloads back to back, padded with one byte to an odd length if needed. A single binary payload that
/// fills <c>PLB</c> is used without copying, in both directions.
/// </summary>
/// <remarks>
/// C-DEngine drops an empty <c>PLB</c>, so empty binary payloads take no bytes; their lengths tell them apart.
/// C-DEngine (6.112.2 and older) loses a <c>PLB</c> whose length is a whole multiple of its chunk size: it announces
/// one chunk more than it sends. Its chunk sizes are even, so an odd length is never affected.
/// </remarks>
internal sealed class TsmPlbLayout : ITsmPlbLayout
{
    public byte[]? Pack(IReadOnlyList<byte[]> binaryPayloads)
    {
        var parts = binaryPayloads.Where(b => b.Length > 0).ToList();
        var length = parts.Sum(p => p.Length);
        if (length == 0) return null;
        if (parts.Count == 1 && length == PaddedLength(length)) return parts[0];

        var plb = new byte[PaddedLength(length)];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(plb, offset);
            offset += part.Length;
        }
        return plb;
    }

    public IReadOnlyList<byte[]>? Unpack(byte[]? plb, IReadOnlyList<int> lengths)
    {
        var available = plb?.Length ?? 0;
        var offset = 0;
        var binaryPayloads = new List<byte[]>(lengths.Count);
        foreach (var length in lengths)
        {
            if (length < 0 || length > available - offset) return null;
            binaryPayloads.Add(Slice(plb, offset, length));
            offset += length;
        }
        return available == PaddedLength(offset) ? binaryPayloads : null;
    }

    private static int PaddedLength(int length) => length == 0 ? 0 : length | 1;

    private static byte[] Slice(byte[]? plb, int offset, int length)
    {
        if (length == 0) return [];
        return length == plb!.Length ? plb : plb.AsSpan(offset, length).ToArray();
    }
}
