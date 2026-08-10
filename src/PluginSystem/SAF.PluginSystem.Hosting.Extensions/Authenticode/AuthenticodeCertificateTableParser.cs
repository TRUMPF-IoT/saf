// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Authenticode;

using System.Buffers.Binary;

internal sealed class AuthenticodeCertificateTableParser : IAuthenticodeCertificateTableParser
{
    private const int WinCertificateHeaderSize = 8;

    public bool IsWellFormed(Stream stream, long offset, long size)
    {
        if (offset < 0 || size <= 0)
        {
            return false;
        }

        var remaining = size;
        var currentOffset = offset;
        Span<byte> header = stackalloc byte[WinCertificateHeaderSize];

        while (remaining > 0)
        {
            if (remaining < WinCertificateHeaderSize)
            {
                return false;
            }

            stream.Position = currentOffset;
            try
            {
                stream.ReadExactly(header);
            }
            catch (EndOfStreamException)
            {
                return false;
            }

            var length = BinaryPrimitives.ReadUInt32LittleEndian(header);
            if (length < WinCertificateHeaderSize || length > remaining)
            {
                return false;
            }

            var alignedLength = ((long)length + 7) & ~7L;
            if (alignedLength > remaining || !HasZeroPadding(stream, currentOffset + length, alignedLength - length))
            {
                return false;
            }

            currentOffset += alignedLength;
            remaining -= alignedLength;
        }

        return remaining == 0;
    }

    public bool TryExtractPkcsSignedData(ReadOnlySpan<byte> certificateBlob, out byte[]? signedData)
    {
        const ushort winCertificateTypePkcsSignedData = 0x0002;
        signedData = null;
        var offset = 0;

        while (offset < certificateBlob.Length)
        {
            if (certificateBlob.Length - offset < WinCertificateHeaderSize)
            {
                return false;
            }

            var length = BinaryPrimitives.ReadUInt32LittleEndian(certificateBlob[offset..]);
            if (length < WinCertificateHeaderSize || length > (uint)(certificateBlob.Length - offset))
            {
                return false;
            }

            var alignedLength = ((long)length + 7) & ~7L;
            if (alignedLength > certificateBlob.Length - offset ||
                !HasZeroPadding(certificateBlob, offset + (int)length, (int)(alignedLength - length)))
            {
                return false;
            }

            var certificateType = BinaryPrimitives.ReadUInt16LittleEndian(certificateBlob[(offset + 6)..]);
            if (certificateType == winCertificateTypePkcsSignedData && signedData is null)
            {
                signedData = certificateBlob
                    .Slice(offset + WinCertificateHeaderSize, (int)length - WinCertificateHeaderSize)
                    .ToArray();
            }

            offset += (int)alignedLength;
        }

        return signedData is not null;
    }

    private bool HasZeroPadding(Stream stream, long offset, long size)
    {
        if (size == 0)
        {
            return true;
        }

        Span<byte> padding = stackalloc byte[7];
        stream.Position = offset;
        try
        {
            stream.ReadExactly(padding[..(int)size]);
        }
        catch (EndOfStreamException)
        {
            return false;
        }

        return padding[..(int)size].IndexOfAnyExcept((byte)0) < 0;
    }

    private bool HasZeroPadding(ReadOnlySpan<byte> buffer, int offset, int size)
    {
        return size == 0 || buffer.Slice(offset, size).IndexOfAnyExcept((byte)0) < 0;
    }
}